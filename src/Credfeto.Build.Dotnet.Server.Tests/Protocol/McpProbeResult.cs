using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Tests.Protocol;

[DebuggerDisplay("Tools={ToolNames.Count}, AdvertisesTools={AdvertisesTools}")]
internal sealed record McpProbeResult(IReadOnlyList<string> ToolNames, bool AdvertisesTools, bool AdvertisesListChanged, string? Instructions);
