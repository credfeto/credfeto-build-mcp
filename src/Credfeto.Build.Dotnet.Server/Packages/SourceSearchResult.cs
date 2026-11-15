using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Packages;

[DebuggerDisplay("Source={Source}, Versions={Versions.Count}")]
public sealed record SourceSearchResult(string Source, IReadOnlyList<string> Versions);
