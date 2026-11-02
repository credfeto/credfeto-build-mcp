using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

/// <summary>
///     One process to run: a fixed executable and argument vector, never a shell command line.
/// </summary>
[DebuggerDisplay("Name={Name}, FileName={FileName}, WorkingDirectory={WorkingDirectory}")]
public sealed record ProcessStep(
    string Name,
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment
)
{
    public string CommandLine => this.FileName + " " + string.Join(separator: ' ', values: this.Arguments);
}
