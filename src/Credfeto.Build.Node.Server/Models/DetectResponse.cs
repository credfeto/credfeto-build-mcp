using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Models;

[DebuggerDisplay("InstallRoots={InstallRoots.Count}, Packages={Packages.Count}")]
public sealed record DetectResponse(
    string RepositoryRoot,
    string PackageManager,
    IReadOnlyList<DetectedInstallRoot> InstallRoots,
    IReadOnlyList<DetectedPackage> Packages,
    int TimeoutMinutes,
    string? OverridesError
);
