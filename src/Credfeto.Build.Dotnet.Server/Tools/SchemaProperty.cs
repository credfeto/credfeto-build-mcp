using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>One string parameter: either an enum of fixed values or a pattern-checked identifier.</summary>
[DebuggerDisplay("Name={Name}, Required={Required}")]
public sealed record SchemaProperty(
    string Name,
    string Description,
    IReadOnlyList<string>? Values,
    string? Pattern,
    string? DefaultValue,
    bool Required
);
