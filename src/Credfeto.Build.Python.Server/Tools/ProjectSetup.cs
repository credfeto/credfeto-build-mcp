using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Server.Commands;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Tools;

// Works out, when a tool is called, what a project's job needs: the interpreter and its version (checked against
// requires-python), a venv that git ignores, requirements that cannot be read as options, and whether the venv is
// already installed from the current manifests
internal static class ProjectSetup
{
    private const string VERSION_SCRIPT = "import sys; print('%d.%d.%d' % sys.version_info[:3])";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    public static async ValueTask<IReadOnlyList<InstallContext>> PrepareAsync(
        ToolContext context,
        PythonRepository repository,
        IReadOnlyList<PythonProject> projects,
        CancellationToken cancellationToken
    )
    {
        string interpreter = context.Python.Require(PythonLocator.PYTHON);
        PythonVersion version = await VersionAsync(
            context: context,
            interpreter: interpreter,
            repositoryRoot: repository.RepositoryRoot,
            cancellationToken: cancellationToken
        );
        List<InstallContext> prepared = [];

        foreach (PythonProject project in projects)
        {
            prepared.Add(
                await PrepareAsync(
                    context: context,
                    repository: repository,
                    project: project,
                    interpreter: interpreter,
                    version: version,
                    cancellationToken: cancellationToken
                )
            );
        }

        return prepared;
    }

    // The install steps for a project whose venv is missing or was installed from other manifests
    public static IReadOnlyList<ProcessStep> InstallIfNeeded(InstallContext context)
    {
        return context.IsCurrent ? [] : PythonCommands.Install(context);
    }

    private static async ValueTask<InstallContext> PrepareAsync(
        ToolContext context,
        PythonRepository repository,
        PythonProject project,
        string interpreter,
        PythonVersion version,
        CancellationToken cancellationToken
    )
    {
        CheckRequiresPython(project: project, version: version);
        CheckRequirements(project);
        await CheckVenvIgnoredAsync(repository: repository, project: project, cancellationToken: cancellationToken);

        VirtualEnvironment environment = VirtualEnvironment.For(
            repositoryRoot: repository.RepositoryRoot,
            project: project
        );
        string stamp = await VirtualEnvironment.StampAsync(
            environment: environment,
            project: project,
            interpreter: interpreter,
            version: version,
            cancellationToken: cancellationToken
        );

        return new(
            RepositoryRoot: repository.RepositoryRoot,
            Project: project,
            Interpreter: interpreter,
            Version: version,
            Environment: environment,
            Stamp: stamp
        )
        {
            VenvExists = System.IO.File.Exists(environment.Python) && System.IO.File.Exists(environment.StampPath),
            IsCurrent = await environment.IsCurrentAsync(stamp: stamp, cancellationToken: cancellationToken),
            Uv = project.Installer == PythonInstaller.Uv ? context.Python.Require(PythonLocator.UV) : null,
        };
    }

    private static async ValueTask<PythonVersion> VersionAsync(
        ToolContext context,
        string interpreter,
        string repositoryRoot,
        CancellationToken cancellationToken
    )
    {
        CommandOutput output = await context.Commands.RunAsync(
            step: new(
                Name: "python version",
                FileName: interpreter,
                Arguments: ["-c", VERSION_SCRIPT],
                WorkingDirectory: repositoryRoot,
                Environment: new Dictionary<string, string>(StringComparer.Ordinal)
            ),
            timeout: ProbeTimeout,
            cancellationToken: cancellationToken
        );

        return output.Succeeded && PythonVersion.Parse(output.StandardOutput) is { } version
            ? version
            : throw new ToolUnavailableException("python3 did not report its version");
    }

    private static void CheckRequiresPython(PythonProject project, PythonVersion version)
    {
        if (
            project.RequiresPython is { } specifiers
            && RequiresPython.Allows(specifiers: specifiers, version: version) == false
        )
        {
            throw new ToolUnavailableException(
                "python3 " + version.Text + " does not satisfy the requires-python of " + project.Name
            );
        }
    }

    // Requirements reach pip as arguments, so one that does not start with a distribution name (an option such as
    // --index-url) is refused rather than passed on
    private static void CheckRequirements(PythonProject project)
    {
        if (
            project
                .Dependencies.Concat(project.TestRequirements)
                .Any(static requirement => RequirementName.Of(requirement) is null)
        )
        {
            throw new ToolUnavailableException(
                "A requirement in the pyproject.toml of " + project.Name + " does not start with a distribution name"
            );
        }
    }

    // The venv must never be committed, so a project whose .venv git would not ignore is refused. The trailing slash
    // makes git match a directory-only pattern such as ".venv/" even before the venv exists
    private static async ValueTask CheckVenvIgnoredAsync(
        PythonRepository repository,
        PythonProject project,
        CancellationToken cancellationToken
    )
    {
        string relative = ProjectReader.Join(
            directory: project.RelativeDirectory,
            file: VirtualEnvironment.DIRECTORY_NAME
        );
        _ = await GitCommand.TryRunAsync(
            repositoryRoot: repository.RepositoryRoot,
            arguments: ["check-ignore", "-q", "--", relative + "/"],
            cancellationToken: cancellationToken
        )
            ?? throw new ToolUnavailableException(
                relative
                    + " is not ignored by git; add .venv/ to .gitignore so the virtual environment is never committed"
            );
    }
}
