using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Testing;

[DebuggerDisplay("Tools={ToolNames.Count}, AdvertisesTools={AdvertisesTools}")]
public sealed record McpProbeResult(
    IReadOnlyList<string> ToolNames,
    bool AdvertisesTools,
    bool AdvertisesListChanged,
    string? Instructions
);
