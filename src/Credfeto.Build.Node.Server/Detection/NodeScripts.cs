using System.Collections.Generic;

namespace Credfeto.Build.Node.Server.Detection;

// The package.json scripts the server records; the rest are not its business
public static class NodeScripts
{
    public const string BUILD = "build";
    public const string LINT = "lint";
    public const string TEST = "test";
    public const string TEST_NO_E2E = "test:noe2e";
    public const string TEST_COVERAGE = "test:coverage";

    public static IReadOnlyList<string> Known { get; } = [BUILD, LINT, TEST, TEST_NO_E2E, TEST_COVERAGE];
}
