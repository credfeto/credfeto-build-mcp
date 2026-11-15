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

    // The executable started directly with the argument vector (no shell) and every stream redirected
    public ProcessStartInfo CreateStartInfo()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = this.FileName,
            WorkingDirectory = this.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in this.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string> variable in this.Environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
    }
}
