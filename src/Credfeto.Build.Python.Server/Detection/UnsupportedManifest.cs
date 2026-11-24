using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Detection;

// A file that means a dependency manager the server does not support
[DebuggerDisplay("{FileName} ({Manager})")]
public sealed record UnsupportedManifest(string FileName, string Manager);
