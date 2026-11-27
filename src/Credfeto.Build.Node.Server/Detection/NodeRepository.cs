using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Detection;

[DebuggerDisplay("InstallRoots={InstallRoots.Count}, Packages={Packages.Count}, Timeout={Timeout}")]
public sealed record NodeRepository(
    string RepositoryRoot,
    IReadOnlyList<NodeInstallRoot> InstallRoots,
    IReadOnlyList<NodePackage> Packages,
    TimeSpan Timeout
)
{
    // node.testScript from the overrides: the script test runs instead of the default choice, where a package has it
    public string? TestScript { get; init; }

    // node.coverageScript: the script coverage runs instead of test:coverage, where a package has it
    public string? CoverageScript { get; init; }

    // node.coverageFile: where a package's Cobertura report is written, relative to the package's directory;
    // null for the Vitest default
    public string? CoverageFile { get; init; }

    // node.memoryPerWorkerMb: the memory each test worker is budgeted, in place of the automatic budget; null when
    // unset or not a positive number
    public int? MemoryPerWorkerMb { get; init; }
}
