using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("Name={Name}, FileName={FileName}, WorkingDirectory={WorkingDirectory}")]
public sealed record ProcessStep(
    string Name,
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment
)
{
    // When set, a failure of this step ends the job even if the job continues on failure
    public bool StopJobOnFailure { get; init; }

    public string CommandLine =>
        this.Arguments.Count == 0
            ? this.FileName
            : this.FileName + " " + string.Join(separator: ' ', values: this.Arguments);
}
