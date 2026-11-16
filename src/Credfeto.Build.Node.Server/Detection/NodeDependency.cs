using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Detection;

[DebuggerDisplay("Name={Name}, Section={Section}")]
public sealed record NodeDependency(string Name, DependencySection Section);
