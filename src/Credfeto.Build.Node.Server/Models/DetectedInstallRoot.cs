using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Models;

[DebuggerDisplay("Directory={Directory}, LockFile={LockFile}")]
public sealed record DetectedInstallRoot(string Directory, string LockFile);
