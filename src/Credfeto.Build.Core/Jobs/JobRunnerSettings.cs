using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("LogDirectory={LogDirectory}, MaxLogCharacters={MaxLogCharacters}, TailLines={TailLines}")]
public sealed record JobRunnerSettings(
    string LogDirectory,
    long MaxLogCharacters,
    int MaxLineLength,
    int TailLines,
    int MaxFailedTests,
    int MaxRetainedJobs
)
{
    public static JobRunnerSettings Create(string logDirectory)
    {
        return new(
            LogDirectory: logDirectory,
            MaxLogCharacters: 16L * 1024 * 1024,
            MaxLineLength: 4096,
            TailLines: 40,
            MaxFailedTests: 50,
            MaxRetainedJobs: 20
        );
    }
}
