using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Commands;

// One npm run of a script in a package. PathFilter is set when an install root's own package runs its script for one
// of its workspaces: the workspace's directory goes to the script's runner as its path filter, so the root's runner
// configuration and setup apply to that workspace's tests only
[DebuggerDisplay("Package={Package.Name}, Script={Script}, PathFilter={PathFilter}")]
public sealed record ScriptRun(NodePackage Package, string Script)
{
    public string? PathFilter { get; init; }

    // Handed to the script's runner ahead of any path filter
    public IReadOnlyList<string> RunnerArguments { get; init; } = [];
}
