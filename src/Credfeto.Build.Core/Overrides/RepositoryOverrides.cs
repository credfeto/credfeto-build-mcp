using System.Diagnostics;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Dotnet={Dotnet}, Node={Node}")]
public sealed record RepositoryOverrides(DotnetOverrides? Dotnet, NodeOverrides? Node)
{
    public static RepositoryOverrides None { get; } = new(Dotnet: null, Node: null);
}
