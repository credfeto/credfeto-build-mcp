using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Credfeto.Build.Node.Server.Detection;

// A package: its name (or its directory when package.json has none), its directory relative to the repository root
// ("." for the root) and which of the scripts the server knows about it has. InstallRoot is the directory of the
// install root it is installed from, so a workspace package runs from there with --workspace
[DebuggerDisplay("Name={Name}, RelativeDirectory={RelativeDirectory}, Scripts={Scripts.Count}")]
public sealed record NodePackage(string Name, string RelativeDirectory, IReadOnlyList<string> Scripts)
{
    public const string ROOT_DIRECTORY = ".";

    public string InstallRoot { get; init; } = ROOT_DIRECTORY;

    public IReadOnlyList<string> ScriptNames { get; init; } = [];

    public IReadOnlyList<NodeDependency> Dependencies { get; init; } = [];

    public NodeTestRunner TestRunner { get; init; }

    // The root package of its install root, as opposed to one of its workspaces
    public bool IsInstallRoot => StringComparer.Ordinal.Equals(x: this.RelativeDirectory, y: this.InstallRoot);

    public bool HasScript(string name)
    {
        return this.Scripts.Contains(value: name, comparer: StringComparer.Ordinal)
            || this.ScriptNames.Contains(value: name, comparer: StringComparer.Ordinal);
    }

    public NodeDependency? FindDependency(string name)
    {
        return this.Dependencies.FirstOrDefault(dependency =>
            StringComparer.Ordinal.Equals(x: dependency.Name, y: name)
        );
    }
}
