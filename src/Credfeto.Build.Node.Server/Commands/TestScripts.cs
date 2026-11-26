using System;
using System.Linq;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Commands;

// The script test runs for a package. all prefers test. unit runs test:noe2e, as the pre-commit hook does: the
// package's own, otherwise its install root's, limited to the package's directory. unit never falls back to a
// workspace's test, which may run its end-to-end tests too; only an install root's own package without test:noe2e
// falls back to test. node.testScript replaces that choice, but only for a package that already has the script: an
// override selects, it never adds
public static class TestScripts
{
    public const string SCOPE_UNIT = "unit";
    public const string SCOPE_ALL = "all";

    public static ScriptRun? For(NodeRepository repository, NodePackage package, string scope, string? overrideScript)
    {
        if (overrideScript is not null)
        {
            return Own(package: package, script: package.HasScript(overrideScript) ? overrideScript : null);
        }

        if (!StringComparer.Ordinal.Equals(x: scope, y: SCOPE_UNIT))
        {
            return Own(
                package: package,
                script: First(package: package, first: NodeScripts.TEST, second: NodeScripts.TEST_NO_E2E)
            );
        }

        if (package.IsInstallRoot)
        {
            return Own(
                package: package,
                script: First(package: package, first: NodeScripts.TEST_NO_E2E, second: NodeScripts.TEST)
            );
        }

        return package.HasScript(NodeScripts.TEST_NO_E2E)
            ? new(Package: package, Script: NodeScripts.TEST_NO_E2E)
            : FromInstallRoot(repository: repository, package: package);
    }

    private static ScriptRun? FromInstallRoot(NodeRepository repository, NodePackage package)
    {
        NodePackage? root = repository.Packages.FirstOrDefault(candidate =>
            candidate.IsInstallRoot
            && StringComparer.Ordinal.Equals(x: candidate.RelativeDirectory, y: package.InstallRoot)
        );

        return root is not null && root.HasScript(NodeScripts.TEST_NO_E2E)
            ? new(Package: root, Script: NodeScripts.TEST_NO_E2E)
            {
                PathFilter = NpmCommands.DirectoryInInstallRoot(package),
            }
            : null;
    }

    private static ScriptRun? Own(NodePackage package, string? script)
    {
        return script is null ? null : new(Package: package, Script: script);
    }

    private static string? First(NodePackage package, string first, string second)
    {
        if (package.HasScript(first))
        {
            return first;
        }

        return package.HasScript(second) ? second : null;
    }
}
