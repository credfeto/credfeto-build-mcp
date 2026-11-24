using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Enabled={Enabled}, Projects={Projects?.Count}, TimeoutMinutes={TimeoutMinutes}")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PythonOverrides(bool? Enabled, IReadOnlyList<string>? Projects, int? TimeoutMinutes);
