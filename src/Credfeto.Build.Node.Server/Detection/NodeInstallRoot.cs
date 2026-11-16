using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Detection;

// A directory npm installs in: a package.json with its npm lock file, both relative to the repository root
[DebuggerDisplay("RelativeDirectory={RelativeDirectory}, LockFile={LockFile}")]
public sealed record NodeInstallRoot(string RelativeDirectory, string LockFile);
