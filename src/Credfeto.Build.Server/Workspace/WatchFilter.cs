using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Server.Workspace;

// Which changes the repository watcher passes on: files with one of the extensions or file names (and the overrides
// file), and directories, outside the ignored directories. By default it watches the whole repository; a
// non-recursive filter watches only the listed directories themselves, so a language whose dependencies live in a
// huge tree (node_modules) does not spend one kernel watch per directory on it
[DebuggerDisplay(
    "Extensions={Extensions.Count}, FileNames={FileNames.Count}, Recursive={Recursive}, Directories={Directories.Count}"
)]
public sealed record WatchFilter(IReadOnlyList<string> Extensions, IReadOnlyList<string> IgnoredDirectories)
{
    // Exact file names that count as well as the extensions, such as a manifest or a lock file
    public IReadOnlyList<string> FileNames { get; init; } = [];

    // Whether each listed directory is watched with everything under it, or on its own
    public bool Recursive { get; init; } = true;

    // The directories to watch, relative to the repository root ("" is the root). One that does not exist yet is
    // watched once it is created in a watched directory
    public IReadOnlyList<string> Directories { get; init; } = [""];
}
