using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("Name={Name}, ExitCode={ExitCode}, DurationSeconds={DurationSeconds}")]
public sealed record StepResult(string Name, string CommandLine, int ExitCode, double DurationSeconds);
