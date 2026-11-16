using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Models;

[DebuggerDisplay("Name={Name}, Directory={Directory}, Scripts={Scripts.Count}")]
public sealed record DetectedPackage(
    string Name,
    string Directory,
    string InstallRoot,
    IReadOnlyList<string> Scripts,
    string TestRunner
);
