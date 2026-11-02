using System.Diagnostics;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Overrides={Overrides}, Error={Error}")]
public sealed record OverridesLoadResult(RepositoryOverrides Overrides, string? Error);
