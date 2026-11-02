using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Dotnet={Dotnet}, Node={Node}, Python={Python}")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepositoryOverrides(LanguageOverrides? Dotnet, LanguageOverrides? Node, LanguageOverrides? Python)
{
    public static RepositoryOverrides None { get; } = new(Dotnet: null, Node: null, Python: null);
}
