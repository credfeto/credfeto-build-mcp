using System;
using System.Collections.Generic;
using System.IO;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Commands;

// The npm command vectors the tools run. npm is started directly with an argument vector, never through a shell,
// and never npx, so nothing is downloaded and run on the fly
public static class NpmCommands
{
    public const string NODE_MODULES = "node_modules";

    // npm writes this after every install; a lock file newer than it means node_modules is out of date
    public const string INSTALLED_LOCK_FILE = ".package-lock.json";

    private const string WORKSPACE_SWITCH = "--workspace=";

    private static readonly IReadOnlyList<string> QuietInstallOptions = ["--ignore-scripts", "--no-audit", "--no-fund"];

    private static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>(
        StringComparer.Ordinal
    )
    {
        // CI=true stops a bare vitest from going into watch mode, and makes other runners non-interactive
        ["CI"] = "true",
        ["NO_COLOR"] = "1",
        ["FORCE_COLOR"] = "0",
        ["npm_config_update_notifier"] = "false",
        ["npm_config_fund"] = "false",
        ["npm_config_audit"] = "false",
    };

    // A clean install from the lock file; it stops the job when it fails, so nothing runs against a broken install
    public static ProcessStep Ci(NodeRepository repository, NodeInstallRoot installRoot, string npm)
    {
        return Step(
            name: "restore " + installRoot.RelativeDirectory,
            npm: npm,
            workingDirectory: FullPath(repository: repository, relativeDirectory: installRoot.RelativeDirectory),
            arguments: ["ci", .. QuietInstallOptions]
        ) with
        {
            StopJobOnFailure = true,
        };
    }

    // The clean install, only when node_modules is missing or older than the lock file, so a test run does not
    // reinstall every time
    public static IReadOnlyList<ProcessStep> InstallIfNeeded(
        NodeRepository repository,
        NodeInstallRoot installRoot,
        string npm
    )
    {
        return NeedsInstall(repository: repository, installRoot: installRoot)
            ? [Ci(repository: repository, installRoot: installRoot, npm: npm)]
            : [];
    }

    public static bool NeedsInstall(NodeRepository repository, NodeInstallRoot installRoot)
    {
        string root = FullPath(repository: repository, relativeDirectory: installRoot.RelativeDirectory);
        FileInfo installed = new(Path.Combine(path1: root, path2: NODE_MODULES, path3: INSTALLED_LOCK_FILE));
        FileInfo lockFile = new(
            Path.Combine(
                path1: repository.RepositoryRoot,
                path2: installRoot.LockFile.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar)
            )
        );

        return !installed.Exists || lockFile.Exists && lockFile.LastWriteTimeUtc > installed.LastWriteTimeUtc;
    }

    // npm run <script>, from the package itself when it is an install root, otherwise from its install root with
    // --workspace so the workspace's scripts see the shared node_modules
    public static ProcessStep RunScript(NodeRepository repository, NodePackage package, string script, string npm)
    {
        return RunScript(repository: repository, run: new(Package: package, Script: script), npm: npm);
    }

    // Runner arguments and a path filter follow --, so npm hands them to the script's runner instead of reading them
    // as its own options
    public static ProcessStep RunScript(NodeRepository repository, ScriptRun run, string npm)
    {
        return Step(
            name: run.PathFilter is null
                ? run.Script + " " + run.Package.Name
                : run.Script + " " + run.Package.Name + " " + run.PathFilter,
            npm: npm,
            workingDirectory: FullPath(repository: repository, relativeDirectory: run.Package.InstallRoot),
            arguments: ["run", run.Script, .. WorkspaceArguments(run.Package), .. RunnerArguments(run)]
        );
    }

    // The package's directory relative to its install root, as --workspace and a runner's path filter take it
    public static string DirectoryInInstallRoot(NodePackage package)
    {
        return StringComparer.Ordinal.Equals(x: package.InstallRoot, y: NodePackage.ROOT_DIRECTORY)
            ? package.RelativeDirectory
            : package.RelativeDirectory[(package.InstallRoot.Length + 1)..];
    }

    // npm install <spec> into one package, saving it to the given section and updating the lock file
    public static ProcessStep Install(
        NodeRepository repository,
        NodePackage package,
        string specification,
        DependencySection section,
        string npm
    )
    {
        return Step(
            name: "install " + specification + " " + package.Name,
            npm: npm,
            workingDirectory: FullPath(repository: repository, relativeDirectory: package.InstallRoot),
            arguments:
            [
                "install",
                specification,
                SaveOption(section),
                .. WorkspaceArguments(package),
                .. QuietInstallOptions,
            ]
        ) with
        {
            StopJobOnFailure = true,
        };
    }

    // Shows which version ended up installed, so the job's output says what changed
    public static ProcessStep List(NodeRepository repository, NodePackage package, string packageId, string npm)
    {
        return Step(
            name: "list " + packageId + " " + package.Name,
            npm: npm,
            workingDirectory: FullPath(repository: repository, relativeDirectory: package.InstallRoot),
            arguments: ["ls", packageId, "--depth=0", .. WorkspaceArguments(package)]
        );
    }

    // The workspace switch takes the package's directory relative to its install root, which works whether or not
    // the package has a name, and as one argument so it can never be read as another switch
    private static IReadOnlyList<string> WorkspaceArguments(NodePackage package)
    {
        if (package.IsInstallRoot)
        {
            return [];
        }

        return [WORKSPACE_SWITCH + DirectoryInInstallRoot(package)];
    }

    private static IReadOnlyList<string> RunnerArguments(ScriptRun run)
    {
        IReadOnlyList<string> arguments = run.PathFilter is null
            ? run.RunnerArguments
            : [.. run.RunnerArguments, run.PathFilter];

        return arguments.Count == 0 ? [] : ["--", .. arguments];
    }

    private static string SaveOption(DependencySection section)
    {
        return section switch
        {
            DependencySection.DevDependencies => "--save-dev",
            DependencySection.OptionalDependencies => "--save-optional",
            _ => "--save-prod",
        };
    }

    private static string FullPath(NodeRepository repository, string relativeDirectory)
    {
        return StringComparer.Ordinal.Equals(x: relativeDirectory, y: NodePackage.ROOT_DIRECTORY)
            ? repository.RepositoryRoot
            : Path.Combine(
                path1: repository.RepositoryRoot,
                path2: relativeDirectory.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar)
            );
    }

    private static ProcessStep Step(string name, string npm, string workingDirectory, IReadOnlyList<string> arguments)
    {
        return new(
            Name: name,
            FileName: npm,
            Arguments: arguments,
            WorkingDirectory: workingDirectory,
            Environment: Environment
        );
    }
}
