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
        IEnumerable<ProcessStep> installs = targets
            .Select(static target => target.Package.InstallRoot)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(installRoot =>
                NpmCommands.InstallIfNeeded(
                    repository: repository,
                    installRoot: PackageChoice.InstallRootOf(repository: repository, installRoot: installRoot),
                    npm: npm
                )
            );

        return
        [
            .. installs,
            .. targets.Select(target =>
                NpmCommands.RunScript(repository: repository, package: target.Package, script: target.Script, npm: npm)
            ),
        ];
    }
}
