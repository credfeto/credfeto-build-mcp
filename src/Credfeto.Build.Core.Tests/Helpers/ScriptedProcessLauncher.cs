using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Core.Tests.Helpers;

/// <summary>A fake process: each step prints its scripted lines and exits with its scripted code.</summary>
internal sealed class ScriptedProcessLauncher : IProcessLauncher
{
    private readonly Dictionary<string, (IReadOnlyList<string> Lines, int ExitCode)> _script = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hanging = new(StringComparer.Ordinal);
    private readonly List<ProcessStep> _started = [];
    private readonly TaskCompletionSource _hangStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<ProcessStep> Started => this._started;

    public Task WaitForHangAsync(CancellationToken cancellationToken)
    {
        return this._hangStarted.Task.WaitAsync(cancellationToken);
    }

    public ScriptedProcessLauncher Prints(string step, int exitCode, params string[] lines)
    {
        this._script[step] = (lines, exitCode);

        return this;
    }

    public ScriptedProcessLauncher Hangs(string step)
    {
        this._hanging.Add(step);

        return this;
    }

    public async ValueTask<int> RunAsync(ProcessStep step, Func<string, ValueTask> onOutputLine, CancellationToken cancellationToken)
    {
        this._started.Add(step);

        if (this._hanging.Contains(step.Name))
        {
            this._hangStarted.TrySetResult();
            await Task.Delay(delay: Timeout.InfiniteTimeSpan, cancellationToken: cancellationToken);
        }

        (IReadOnlyList<string> lines, int exitCode) = this._script.GetValueOrDefault(key: step.Name, defaultValue: ([], 0));

        foreach (string line in lines)
        {
            await onOutputLine(line);
        }

        return exitCode;
    }
}
