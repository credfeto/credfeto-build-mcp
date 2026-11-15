using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Tools;

[DebuggerDisplay("Name={Name}, Required={Required}")]
public sealed record SchemaProperty(
    string Name,
    string Description,
    IReadOnlyList<string>? Values,
    string? Pattern,
    string? DefaultValue,
    bool Required
)
{
    public const string TRUE = "true";
    public const string FALSE = "false";

    public bool Flag { get; init; }
}
