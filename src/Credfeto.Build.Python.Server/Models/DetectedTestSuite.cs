using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Models;

[DebuggerDisplay("Directories={Directories.Count}")]
public sealed record DetectedTestSuite(IReadOnlyList<string> Directories, string Note);
