using System.Diagnostics;

namespace Credfeto.Build.Core.Overrides;

[DebuggerDisplay("Dotnet={Dotnet}, Node={Node}, Python={Python}")]
public sealed record RepositoryOverrides(DotnetOverrides? Dotnet, NodeOverrides? Node, PythonOverrides? Python)
{
    public static RepositoryOverrides None { get; } = new(Dotnet: null, Node: null, Python: null);
}
