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
);
