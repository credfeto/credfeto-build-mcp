using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Detection;

/// <summary>The outcome of looking for .NET in the repository; <see cref="Repository" /> is null when none was found.</summary>
[DebuggerDisplay("RepositoryRoot={RepositoryRoot}, Detected={Repository != null}, Reason={Reason}")]
public sealed record DotnetDetection(string RepositoryRoot, DotnetRepository? Repository, string Reason, string? OverridesError);
