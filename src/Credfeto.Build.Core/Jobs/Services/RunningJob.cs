using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;

namespace Credfeto.Build.Core.Jobs.Services;

internal sealed class RunningJob : IAsyncDisposable
{
    private readonly Lock _lock = new();
    private readonly List<StepResult> _steps = [];
    private readonly TimeProvider _timeProvider;
    private readonly long _started;
    private long? _finished;
    private int _currentStep = -1;
    private JobState _state = JobState.Running;
    private CoverageSummary? _coverage;
    private IReadOnlyList<string> _coverageErrors = [];
    private string? _message;
    private IReadOnlyList<string>? _changedFiles;

    public RunningJob(
        string id,
        long sequence,
        JobDefinition definition,
        string logPath,
        JobOutputCapture capture,
        CancellationTokenSource timeout,
        CancellationTokenSource cancellation,
        TimeProvider timeProvider
    )
    {
        this.Id = id;
        this.Sequence = sequence;
        this.Definition = definition;
        this.LogPath = logPath;
        this.Capture = capture;
        this.Timeout = timeout;
        this.Cancellation = cancellation;
        this._timeProvider = timeProvider;
        this._started = timeProvider.GetTimestamp();
    }

    public string Id { get; }

    public long Sequence { get; }

    public JobDefinition Definition { get; }

    public string LogPath { get; }

    public JobOutputCapture Capture { get; }

    public CancellationTokenSource Timeout { get; }

    public CancellationTokenSource Cancellation { get; }

    public Task Completion { get; set; } = Task.CompletedTask;

    public bool IsRunning
    {
        get
        {
            lock (this._lock)
            {
                return this._state == JobState.Running;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        this.Timeout.Dispose();
        this.Cancellation.Dispose();

        return this.Capture.DisposeAsync();
    }

    public Task WaitAsync(TimeSpan timeout)
    {
        // Used only while shutting down, where the bound is the timeout itself
        return this.Completion.WaitAsync(timeout: timeout, cancellationToken: CancellationToken.None);
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return this.Completion.WaitAsync(cancellationToken);
    }

    public void BeginStep(int index)
    {
        lock (this._lock)
        {
            this._currentStep = index;
        }
    }

    public void CompleteStep(StepResult result)
    {
        lock (this._lock)
        {
            this._steps.Add(result);
        }
    }

    public void SetCoverage(CoverageSummary? coverage, IReadOnlyList<string> errors)
    {
        lock (this._lock)
        {
            this._coverage = coverage;
            this._coverageErrors = errors;
        }
    }

    public void SetChangedFiles(IReadOnlyList<string> changedFiles)
    {
        lock (this._lock)
        {
            this._changedFiles = changedFiles;
        }
    }

    public void Finish(JobState state, string? message)
    {
        lock (this._lock)
        {
            if (this._state != JobState.Running)
            {
                return;
            }

            this._state = state;
            this._message = message;
            this._finished = this._timeProvider.GetTimestamp();
        }
    }

    public JobStatus Status()
    {
        lock (this._lock)
        {
            string? current =
                this._state == JobState.Running && this._currentStep >= 0
                    ? this.Definition.Steps[this._currentStep].Name
                    : null;

            return new(
                Id: this.Id,
                Kind: this.Definition.Kind,
                State: this._state,
                DurationSeconds: this.Elapsed(),
                StepsCompleted: this._steps.Count,
                StepCount: this.Definition.Steps.Count,
                CurrentStep: current,
                LogPath: this.LogPath
            );
        }
    }

    public JobResult Result(int maxFailedTests)
    {
        ITestOutputParser? parser = this.Definition.TestParser;
        IReadOnlyList<string> tail = this.Capture.Tail();

        lock (this._lock)
        {
            IReadOnlyList<string> failed = parser is null ? [] : Limit(values: parser.FailedTests, max: maxFailedTests);

            return new(
                Id: this.Id,
                Kind: this.Definition.Kind,
                State: this._state,
                DurationSeconds: this.Elapsed(),
                LogPath: this.LogPath,
                Steps: [.. this._steps],
                Tests: parser?.Counts,
                FailedTests: failed,
                OutputTail: tail,
                Coverage: this._coverage,
                CoverageMarkdown: this._coverage is null
                    ? null
                    : CoverageMarkdown.Format(summary: this._coverage, headSha: this.Definition.HeadSha),
                CoverageErrors: this._coverageErrors,
                HeadSha: this.Definition.HeadSha,
                Message: this._message,
                ChangedFiles: this._changedFiles
            );
        }
    }

    private static IReadOnlyList<string> Limit(IReadOnlyList<string> values, int max)
    {
        return values.Count <= max ? [.. values] : [.. values.Take(max)];
    }

    private double Elapsed()
    {
        TimeSpan elapsed = this._finished is { } finished
            ? this._timeProvider.GetElapsedTime(startingTimestamp: this._started, endingTimestamp: finished)
            : this._timeProvider.GetElapsedTime(this._started);

        return Math.Round(value: elapsed.TotalSeconds, digits: 1);
    }
}
