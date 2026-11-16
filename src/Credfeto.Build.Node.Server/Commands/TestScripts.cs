using System;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Commands;

// The script test runs for a package. unit prefers test:noe2e, as the pre-commit hook does, and all prefers test.
// node.testScript replaces that choice, but only for a package that already has the script: an override selects,
// it never adds
public static class TestScripts
{
    public const string SCOPE_UNIT = "unit";
    public const string SCOPE_ALL = "all";

    public static string? For(NodePackage package, string scope, string? overrideScript)
    {
        if (overrideScript is not null)
        {
            return package.HasScript(overrideScript) ? overrideScript : null;
        }

        return StringComparer.Ordinal.Equals(x: scope, y: SCOPE_UNIT)
            ? First(package: package, first: NodeScripts.TEST_NO_E2E, second: NodeScripts.TEST)
            : First(package: package, first: NodeScripts.TEST, second: NodeScripts.TEST_NO_E2E);
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
