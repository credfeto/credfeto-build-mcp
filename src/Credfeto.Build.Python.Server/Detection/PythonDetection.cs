using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Detection;

// The outcome of looking for Python projects in the repository; Repository is null when there is none. TestSuite is
// set only when there is no project but there are Python test files. The watched directories are those whose changes
// can change the outcome, relative to the repository root
[DebuggerDisplay("RepositoryRoot={RepositoryRoot}, Detected={Repository != null}, Reason={Reason}")]
public sealed record PythonDetection(
    string RepositoryRoot,
    PythonRepository? Repository,
    string Reason,
    string? OverridesError,
    IReadOnlyList<string> WatchedDirectories
)
{
    public PythonTestSuite? TestSuite { get; init; }
}
