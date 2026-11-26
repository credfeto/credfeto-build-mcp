namespace Credfeto.Build.Node.Server.Detection;

// The test runner a package's test script runs, used to read its output and pick its coverage switch; Unknown
// reports exit codes alone and gets no coverage switch
public enum NodeTestRunner
{
    Unknown,
    Vitest,
    Jest,
    NodeTest,
}
