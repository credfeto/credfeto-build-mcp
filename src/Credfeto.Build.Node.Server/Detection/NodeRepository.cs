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
}
