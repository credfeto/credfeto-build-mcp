using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Detection;

/// <summary>A project in the solution; <see cref="RelativePath" /> is relative to the solution directory.</summary>
[DebuggerDisplay("Name={Name}, RelativePath={RelativePath}, Kind={Kind}")]
public sealed record DotnetProject(string Name, string RelativePath, DotnetProjectKind Kind);
