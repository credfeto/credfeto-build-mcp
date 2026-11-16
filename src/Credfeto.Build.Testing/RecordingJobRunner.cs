using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Exceptions;

namespace Credfeto.Build.Testing;

// Records the jobs a tool starts instead of running them; a test moves a job on by telling the runner its new status
public sealed class RecordingJobRunner : IJobRunner
{
    public const string JOB_ID = "0123456789abcdef0123456789abcdef";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, JobResult> _results = [with(StringComparer.Ordinal)];
    private readonly List<JobDefinition> _started = [];
    private readonly Dictionary<string, JobStatus> _statuses = [with(StringComparer.Ordinal)];
    private readonly TimeProvider _timeProvider;
    private readonly Channel<TimeSpan> _waits = Channel.CreateUnbounded<TimeSpan>();
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RecordingJobRunner()
        : this(TimeProvider.System) { }

    public RecordingJobRunner(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
    }

    public IReadOnlyList<JobDefinition> Started => this._started;

    public bool Busy { get; set; }

    public void Know(JobStatus status)
    {
        TaskCompletionSource changed;

        lock (this._lock)
        {
            this._statuses[status.Id] = status;
            changed = this._changed;
            this._changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.SetResult();
    }

    public void KnowResult(JobResult result)
    {
        lock (this._lock)
        {
            this._results[result.Id] = result;
        }
    }

    // Returns how long the next wait this method has not yet returned for may last, once its timer is running
    public ValueTask<TimeSpan> WaitUntilWaitingAsync(CancellationToken cancellationToken)
    {
        return this._waits.Reader.ReadAsync(cancellationToken);
    }

    public ValueTask<JobStatus> StartAsync(JobDefinition definition)
    {
        if (this.Busy)
        {
            throw new JobAlreadyRunningException();
        }

        this._started.Add(definition);

        return ValueTask.FromResult(
            new JobStatus(
                Id: JOB_ID,
                Kind: definition.Kind,
                State: JobState.Running,
                DurationSeconds: 0,
                StepsCompleted: 0,
                StepCount: definition.Steps.Count,
                CurrentStep: null,
                LogPath: "/logs/" + JOB_ID + ".log"
            )
        );
    }

    public JobStatus? FindStatus(string id)
    {
        lock (this._lock)
        {
            return this._statuses.GetValueOrDefault(id);
        }
    }

    public JobResult? FindResult(string id)
    {
        lock (this._lock)
        {
            return this._results.GetValueOrDefault(id);
        }
    }

    public ValueTask<bool> WaitForCompletionAsync(string id, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(this.FindStatus(id) is { State: not JobState.Running });
    }

    public async ValueTask<bool> WaitForChangeAsync(
        string id,
        int stepsCompleted,
        TimeSpan maxWait,
        CancellationToken cancellationToken
    )
    {
        TaskCompletionSource changed;

        lock (this._lock)
        {
            if (this._statuses.GetValueOrDefault(id) is not { } status)
            {
                return false;
            }

            if (status.State != JobState.Running || status.StepsCompleted != stepsCompleted)
            {
                return status.State != JobState.Running;
            }

            changed = this._changed;
        }

        await this.StartWaitAsync(changed: changed, maxWait: maxWait, cancellationToken: cancellationToken)
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        cancellationToken.ThrowIfCancellationRequested();

        return this.FindStatus(id) is { State: not JobState.Running };
    }

    private Task StartWaitAsync(TaskCompletionSource changed, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        try
        {
            return changed.Task.WaitAsync(
                timeout: maxWait,
                timeProvider: this._timeProvider,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            // Announced only once the timer exists, so a test that advances a fake clock next is sure to trip it
            _ = this._waits.Writer.TryWrite(maxWait);
        }
    }
}
