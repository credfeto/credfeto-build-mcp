using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Models;

[DebuggerDisplay("PackageId={PackageId}, LatestStable={LatestStable}, Sources={Sources.Count}")]
public sealed record PackageVersionsResponse(
    string PackageId,
    bool IncludePrerelease,
    bool SourceMappingConfigured,
    string? LatestStable,
    string? LatestPrerelease,
    IReadOnlyList<PackageSourceVersions> Sources,
    IReadOnlyList<PackageSourceProblem> Problems
);
