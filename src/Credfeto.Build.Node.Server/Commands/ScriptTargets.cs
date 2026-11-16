using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Commands;

// Which packages a script tool runs for. A named package runs on its own. For all, an install root whose own package
// has the script runs it once at the root (as the pre-commit hook does, so a root script that already covers the
// workspaces is not repeated); otherwise each of its workspace packages with the script runs
public static class ScriptTargets
{
    public const string ALL = "all";

    public static IReadOnlyList<(NodePackage Package, string Script)> Select(
        NodeRepository repository,
        string choice,
        Func<NodePackage, string?> script
    )
    {
        IEnumerable<NodePackage> candidates = StringComparer.Ordinal.Equals(x: choice, y: ALL)
            ? AllTargets(repository: repository, script: script)
            : repository.Packages.Where(package => StringComparer.Ordinal.Equals(x: package.Name, y: choice));

        return
        [
            .. candidates
                .Select(package => (Package: package, Script: script(package)))
                .Where(static target => target.Script is not null)
                .Select(static target => (target.Package, target.Script!)),
        ];
    }

    // The package names a script tool can be asked for, sorted, after all
    public static IReadOnlyList<string> Choices(NodeRepository repository, Func<NodePackage, string?> script)
    {
        return
        [
            ALL,
            .. repository
                .Packages.Where(package => script(package) is not null)
                .Select(static package => package.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<NodePackage> AllTargets(NodeRepository repository, Func<NodePackage, string?> script)
    {
        foreach (NodeInstallRoot installRoot in repository.InstallRoots)
        {
            NodePackage[] packages =
            [
                .. repository.Packages.Where(package =>
                    StringComparer.Ordinal.Equals(x: package.InstallRoot, y: installRoot.RelativeDirectory)
                    && script(package) is not null
                ),
            ];

            NodePackage? root = Array.Find(array: packages, match: static package => package.IsInstallRoot);

            foreach (NodePackage package in root is null ? packages : [root])
            {
                yield return package;
            }
        }
    }
}
