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
);
