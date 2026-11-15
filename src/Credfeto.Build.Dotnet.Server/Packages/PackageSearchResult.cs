using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Build.Dotnet.Server.Models;

namespace Credfeto.Build.Dotnet.Server.Packages;

[DebuggerDisplay("Sources={Sources.Count}, Problems={Problems.Count}")]
public sealed record PackageSearchResult(
    IReadOnlyList<SourceSearchResult> Sources,
    IReadOnlyList<PackageSourceProblem> Problems
);
