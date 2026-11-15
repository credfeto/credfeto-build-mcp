using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Server.Workspace;

// Which changes the repository watcher passes on: files with one of the extensions (and the overrides file), and
// directories, anywhere outside the ignored directories
[DebuggerDisplay("Extensions={Extensions.Count}, IgnoredDirectories={IgnoredDirectories.Count}")]
public sealed record WatchFilter(IReadOnlyList<string> Extensions, IReadOnlyList<string> IgnoredDirectories);
