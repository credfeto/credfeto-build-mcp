using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Node.Server.Detection;

// A package: its name (or its directory when package.json has none), its directory relative to the repository root
// ("." for the root) and which of the scripts the server knows about it has
[DebuggerDisplay("Name={Name}, RelativeDirectory={RelativeDirectory}, Scripts={Scripts.Count}")]
public sealed record NodePackage(string Name, string RelativeDirectory, IReadOnlyList<string> Scripts)
{
    public const string ROOT_DIRECTORY = ".";
}
