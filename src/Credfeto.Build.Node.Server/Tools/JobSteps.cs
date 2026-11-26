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
        IEnumerable<ProcessStep> installs = runs.Select(static run => run.Package.InstallRoot)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(installRoot =>
                NpmCommands.InstallIfNeeded(
                    repository: repository,
                    installRoot: PackageChoice.InstallRootOf(repository: repository, installRoot: installRoot),
                    npm: npm
                )
            );

        return [.. installs, .. runs.Select(run => NpmCommands.RunScript(repository: repository, run: run, npm: npm))];
    }
}
