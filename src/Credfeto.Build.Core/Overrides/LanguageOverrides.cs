using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Build.Core.Overrides;

/// <summary>
///     Per-language settings from <c>.ai/commands.json</c>. They can only narrow or select among what detection
///     finds; they can never add a command, an argument or an executable.
/// </summary>
[DebuggerDisplay("Enabled={Enabled}, Solution={Solution}, TimeoutMinutes={TimeoutMinutes}")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LanguageOverrides(
    bool? Enabled,
    string? Solution,
    IReadOnlyList<string>? ExcludeTestProjects,
    int? TimeoutMinutes
);
