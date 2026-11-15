using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Commands;

[DebuggerDisplay("ExitCode={ExitCode}, TimedOut={TimedOut}")]
public sealed record CommandOutput(int ExitCode, string StandardOutput, bool TimedOut)
{
    public const int NOT_STARTED = -1;

    public bool Succeeded => !this.TimedOut && this.ExitCode == 0;
}
