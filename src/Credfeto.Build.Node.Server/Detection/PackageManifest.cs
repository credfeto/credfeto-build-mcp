using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Detection;

// What the server reads from one package.json
[DebuggerDisplay("Name={Name}, Scripts={Scripts.Count}, Workspaces={Workspaces.Count}")]
public sealed record PackageManifest(
    string? Name,
    IReadOnlyList<string> Scripts,
    IReadOnlyList<string> Workspaces,
    string? PackageManager
)
{
    // Every script name, so an override can only select a script the package already has
    public IReadOnlyList<string> ScriptNames { get; init; } = [];

    public IReadOnlyList<NodeDependency> Dependencies { get; init; } = [];

    public NodeTestRunner TestRunner { get; init; }
}
