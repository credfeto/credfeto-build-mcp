using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Models;

[DebuggerDisplay("Source={Source}, UsedByRestore={UsedByRestore}, VersionCount={VersionCount}")]
public sealed record PackageSourceVersions(
    string Source,
    bool UsedByRestore,
    int VersionCount,
    IReadOnlyList<string> Versions,
    string? LatestStable,
    string? LatestPrerelease
);
