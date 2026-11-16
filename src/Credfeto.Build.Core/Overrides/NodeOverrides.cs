using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Enabled={Enabled}, TestScript={TestScript}, CoverageScript={CoverageScript}")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NodeOverrides(
    bool? Enabled,
    IReadOnlyList<string>? Packages,
    string? TestScript,
    string? CoverageScript,
    string? CoverageFile,
    int? TimeoutMinutes
);
