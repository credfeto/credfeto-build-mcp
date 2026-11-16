using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Testing;

// A fake process for each step that runs until the test releases it, then exits 0 with no output; a step that is
// never released ends when the job is cancelled, unless the launcher ignores cancellation, as a process slow to die
public sealed class GatedProcessLauncher : IProcessLauncher
{
    private readonly bool _ignoresCancellation;

    private readonly Dictionary<string, TaskCompletionSource> _gates = [with(StringComparer.Ordinal)];
    private readonly Lock _lock = new();
    private readonly Dictionary<string, TaskCompletionSource> _started = [with(StringComparer.Ordinal)];

    public GatedProcessLauncher(bool ignoresCancellation = false)
    {
        this._ignoresCancellation = ignoresCancellation;
    }

    public static JobDefinition Job(TimeSpan timeout, params string[] steps)
    {
        List<ProcessStep> processSteps = [];

        foreach (string step in steps)
        {
            processSteps.Add(
                new(
                    Name: step,
                    FileName: "/bin/" + step,
                    Arguments: [],
                    WorkingDirectory: "/",
                    Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                )
            );
        }

        return new(
            Kind: "build",
            Steps: processSteps,
            Timeout: timeout,
            ContinueOnFailure: false,
            TestParser: null,
            CoverageFiles: [],
            HeadSha: null
        );
    }

    public Task WaitForStartAsync(string step, CancellationToken cancellationToken)
    {
        return Signal(signals: this._started, name: step, sync: this._lock).Task.WaitAsync(cancellationToken);
    }

    public void Release(string step)
    {
        _ = Signal(signals: this._gates, name: step, sync: this._lock).TrySetResult();
    }

    public async ValueTask<int> RunAsync(
        ProcessStep step,
        Func<string, ValueTask> onOutputLine,
        CancellationToken cancellationToken
    )
    {
        _ = Signal(signals: this._started, name: step.Name, sync: this._lock).TrySetResult();
        await Signal(signals: this._gates, name: step.Name, sync: this._lock)
            .Task.WaitAsync(this._ignoresCancellation ? CancellationToken.None : cancellationToken);

        return 0;
    }

    private static TaskCompletionSource Signal(Dictionary<string, TaskCompletionSource> signals, string name, Lock sync)
    {
        lock (sync)
        {
            if (!signals.TryGetValue(key: name, value: out TaskCompletionSource? signal))
            {
                signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
                signals.Add(key: name, value: signal);
            }

            return signal;
        }
    }
}
