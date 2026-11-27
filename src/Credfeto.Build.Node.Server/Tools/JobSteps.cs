using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Tools;

// The steps of a script job: an install for each install root that needs one, then each package's script
internal static class JobSteps
{
    public static IReadOnlyList<ProcessStep> ForScripts(
        NodeRepository repository,
        IReadOnlyList<(NodePackage Package, string Script)> targets,
        string npm
    )
    {
        return ForRuns(
            repository: repository,
            runs: [.. targets.Select(static target => new ScriptRun(Package: target.Package, Script: target.Script))],
            npm: npm
        );
    }

    public static IReadOnlyList<ProcessStep> ForRuns(
        NodeRepository repository,
        IReadOnlyList<ScriptRun> runs,
        string npm
    )
    {
        return
        [
            .. Installs(repository: repository, packages: runs.Select(static run => run.Package), npm: npm),
            .. runs.Select(run => NpmCommands.RunScript(repository: repository, run: run, npm: npm)),
        ];
    }

    // A target whose script is tsc has no typecheck script of its own, so the local tsc checks its tsconfig.json
    public static IReadOnlyList<ProcessStep> ForTypeChecks(
        NodeRepository repository,
        IReadOnlyList<(NodePackage Package, string Script)> targets,
        string npm
    )
    {
        return
        [
            .. Installs(repository: repository, packages: targets.Select(static target => target.Package), npm: npm),
            .. targets.Select(target =>
                StringComparer.Ordinal.Equals(x: target.Script, y: NpmCommands.TSC)
                    ? NpmCommands.TypeCheck(repository: repository, package: target.Package)
                    : NpmCommands.RunScript(
                        repository: repository,
                        package: target.Package,
                        script: target.Script,
                        npm: npm
                    )
            ),
        ];
    }

    private static IEnumerable<ProcessStep> Installs(
        NodeRepository repository,
        IEnumerable<NodePackage> packages,
        string npm
    )
    {
        return packages
            .Select(static package => package.InstallRoot)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(installRoot =>
                NpmCommands.InstallIfNeeded(
                    repository: repository,
                    installRoot: PackageChoice.InstallRootOf(repository: repository, installRoot: installRoot),
                    npm: npm
                )
            );
    }
}
