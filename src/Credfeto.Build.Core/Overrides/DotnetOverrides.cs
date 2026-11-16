using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Enabled={Enabled}, Solution={Solution}, TimeoutMinutes={TimeoutMinutes}")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DotnetOverrides(
    bool? Enabled,
    string? Solution,
    IReadOnlyList<string>? ExcludeTestProjects,
    int? TimeoutMinutes
);
