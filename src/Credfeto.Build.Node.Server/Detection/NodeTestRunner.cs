namespace Credfeto.Build.Node.Server.Detection;

// The test runner a package's test script runs, used only to read its output; Unknown reports exit codes alone
public enum NodeTestRunner
{
    Unknown,
    Vitest,
    Jest,
    NodeTest,
}
