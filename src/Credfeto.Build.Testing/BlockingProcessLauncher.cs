using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Testing;

// A fake process that holds each step until the test releases it, so a test decides exactly when a job moves on
public sealed class BlockingProcessLauncher : IProcessLauncher
{
    private readonly Lock _lock = new();
    private readonly Channel<string> _started = Channel.CreateUnbounded<string>();
    private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Returns the name of the next step to start that this method has not yet returned
    public ValueTask<string> WaitForStepAsync(CancellationToken cancellationToken)
    {
        return this._started.Reader.ReadAsync(cancellationToken);
    }

    // Lets every step that has started so far exit successfully
    public void Release()
    {
        TaskCompletionSource release;

        lock (this._lock)
        {
            release = this._release;
            this._release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        release.SetResult();
    }

    public async ValueTask<int> RunAsync(
        ProcessStep step,
        Func<string, ValueTask> onOutputLine,
        CancellationToken cancellationToken
    )
    {
        TaskCompletionSource release = this.CurrentRelease();

        _ = this._started.Writer.TryWrite(step.Name);
        await release.Task.WaitAsync(cancellationToken);

        return 0;
    }

    private TaskCompletionSource CurrentRelease()
    {
        lock (this._lock)
        {
            return this._release;
        }
    }
}
