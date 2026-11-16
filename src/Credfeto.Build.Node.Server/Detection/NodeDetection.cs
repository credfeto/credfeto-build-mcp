using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Detection;

// The outcome of looking for npm packages in the repository; Repository is null when none is supported. The watched
// directories are those whose changes can change the outcome, relative to the repository root
[DebuggerDisplay("RepositoryRoot={RepositoryRoot}, Detected={Repository != null}, Reason={Reason}")]
public sealed record NodeDetection(
    string RepositoryRoot,
    NodeRepository? Repository,
    string Reason,
    string? OverridesError,
    IReadOnlyList<string> WatchedDirectories
);
