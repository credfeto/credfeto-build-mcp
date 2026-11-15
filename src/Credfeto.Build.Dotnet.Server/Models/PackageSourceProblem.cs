using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Models;

[DebuggerDisplay("Source={Source}, Text={Text}")]
public sealed record PackageSourceProblem(string? Source, string Text);
