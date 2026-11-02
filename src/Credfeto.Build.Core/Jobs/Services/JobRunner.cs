using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Core.Coverage.Exceptions;
using Credfeto.Build.Core.Jobs.Exceptions;

namespace Credfeto.Build.Core.Jobs.Services;

/// <summary>
///     Runs one job at a time in the background, so no MCP call waits on a build; callers poll by job id.
/// </summary>
public sealed class JobRunner : IJobRunner, IAsyncDisposable
{
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, RunningJob> _jobs = new(StringComparer.Ordinal);
    private readonly IProcessLauncher _launcher;
    private readonly Lock _lock = new();
    private readonly IOutputMasker _masker;
    private readonly JobRunnerSettings _settings;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TimeProvider _timeProvider;
    private long _sequence;

    public JobRunner(
        IProcessLauncher launcher,
        IOutputMasker masker,
        JobRunnerSettings settings,
        TimeProvider timeProvider
    )
    {
        this._launcher = launcher;
        this._masker = masker;
        this._settings = settings;
        this._timeProvider = timeProvider;
    }

    public async ValueTask DisposeAsync()
    {
        await this._shutdown.CancelAsync();

        foreach (RunningJob job in this.Snapshot())
        {
            // Every job records its own end state, so a fault or a slow kill here has nothing left to report
            await job.WaitAsync(ShutdownWait).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await job.DisposeAsync();
        }

        this._shutdown.Dispose();
    }

    public async ValueTask<JobStatus> StartAsync(JobDefinition definition)
    {
        RunningJob job;
        RunningJob[] evicted;

        lock (this._lock)
        {
            if (this._jobs.Values.Any(static existing => existing.IsRunning))
            {
                throw new JobAlreadyRunningException();
            }

            evicted = this.EvictFinishedJobs();
            job = this.CreateJob(definition);
            this._jobs.Add(key: job.Id, value: job);
            job.Completion = this.ExecuteAsync(job);
        }

        foreach (RunningJob old in evicted)
        {
            await old.DisposeAsync();
        }

        return job.Status();
    }

    public JobStatus? FindStatus(string id)
    {
        return this.Find(id)?.Status();
    }

    public JobResult? FindResult(string id)
    {
        return this.Find(id)?.Result(this._settings.MaxFailedTests);
    }

    public async ValueTask WaitForCompletionAsync(string id, CancellationToken cancellationToken)
    {
        RunningJob? job = this.Find(id);

        if (job is not null)
        {
            await job.WaitAsync(cancellationToken);
        }
    }

    private RunningJob? Find(string id)
    {
        lock (this._lock)
        {
            return this._jobs.GetValueOrDefault(id);
        }
    }

    private RunningJob[] Snapshot()
    {
        lock (this._lock)
        {
            return [.. this._jobs.Values];
        }
    }

    private RunningJob CreateJob(JobDefinition definition)
    {
        string id = Guid.NewGuid().ToString(format: "N", provider: CultureInfo.InvariantCulture);
        string logPath = Path.Combine(path1: this._settings.LogDirectory, path2: id + ".log");
        CreateLogDirectory(this._settings.LogDirectory);

        JobOutputCapture capture = new(
            logPath: logPath,
            masker: this._masker,
            parser: definition.TestParser,
            settings: this._settings
        );

        // Created before the job starts, so the timeout runs from the moment the job is accepted
        CancellationTokenSource timeout = new(delay: definition.Timeout, timeProvider: this._timeProvider);
        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            token1: timeout.Token,
            token2: this._shutdown.Token
        );

        this._sequence++;

        return new(
            id: id,
            sequence: this._sequence,
            definition: definition,
            logPath: logPath,
            capture: capture,
            timeout: timeout,
            cancellation: cancellation,
            timeProvider: this._timeProvider
        );
    }

    private async Task ExecuteAsync(RunningJob job)
    {
        // Leave the caller's lock before any step runs
        await Task.Yield();

        try
        {
            bool failed = await this.RunStepsAsync(job);
            await CollectCoverageAsync(job: job, cancellationToken: job.Cancellation.Token);
            await CollectChangedFilesAsync(job: job, cancellationToken: job.Cancellation.Token);
            job.Finish(state: failed ? JobState.Failed : JobState.Succeeded, message: null);
        }
        catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
        {
            await EndCancelledAsync(job);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            await job.Capture.WriteHeaderAsync("[job failed to run: " + exception.Message + "]");
            job.Finish(state: JobState.Failed, message: "A process could not be started: " + exception.Message);
        }
        finally
        {
            // Anything unexpected still ends the job, so it can never block the next one
            job.Finish(state: JobState.Failed, message: "The job ended unexpectedly");
        }
    }

    private static async ValueTask EndCancelledAsync(RunningJob job)
    {
        if (job.Timeout.IsCancellationRequested)
        {
            string minutes = job.Definition.Timeout.TotalMinutes.ToString(provider: CultureInfo.InvariantCulture);
            await job.Capture.WriteHeaderAsync("[job timed out]");
            job.Finish(state: JobState.TimedOut, message: "The job exceeded its timeout of " + minutes + " minutes and was killed");

            return;
        }

        await job.Capture.WriteHeaderAsync("[job cancelled]");
        job.Finish(state: JobState.Cancelled, message: "The server shut down while the job was running");
    }

    private async ValueTask<bool> RunStepsAsync(RunningJob job)
    {
        bool failed = false;
        CancellationToken cancellationToken = job.Cancellation.Token;

        for (int index = 0; index < job.Definition.Steps.Count; index++)
        {
            if (failed && !job.Definition.ContinueOnFailure)
            {
                break;
            }

            ProcessStep step = job.Definition.Steps[index];
            job.BeginStep(index);
            await job.Capture.WriteHeaderAsync("$ " + step.CommandLine);

            long started = this._timeProvider.GetTimestamp();
            int exitCode = await this._launcher.RunAsync(
                step: step,
                onOutputLine: job.Capture.WriteLineAsync,
                cancellationToken: cancellationToken
            );
            double duration = Math.Round(value: this._timeProvider.GetElapsedTime(started).TotalSeconds, digits: 1);

            await job.Capture.WriteHeaderAsync("[exit code " + exitCode.ToString(CultureInfo.InvariantCulture) + "]");
            job.CompleteStep(new(Name: step.Name, CommandLine: step.CommandLine, ExitCode: exitCode, DurationSeconds: duration));
            failed |= exitCode != 0;
        }

        return failed;
    }

    private static async ValueTask CollectCoverageAsync(RunningJob job, CancellationToken cancellationToken)
    {
        if (job.Definition.CoverageFiles.Count == 0)
        {
            return;
        }

        List<CoberturaReport> reports = [];
        List<string> errors = [];

        foreach (string file in job.Definition.CoverageFiles)
        {
            if (!File.Exists(file))
            {
                errors.Add("No coverage file was produced: " + Path.GetFileName(file));

                continue;
            }

            try
            {
                reports.Add(await CoberturaParser.ParseFileAsync(path: file, cancellationToken: cancellationToken));
            }
            catch (Exception exception) when (exception is InvalidCoberturaException or IOException)
            {
                errors.Add(Path.GetFileName(file) + ": " + exception.Message);
            }
        }

        job.SetCoverage(coverage: reports.Count == 0 ? null : CoverageAggregator.Combine(reports), errors: errors);
    }

    private static async ValueTask CollectChangedFilesAsync(RunningJob job, CancellationToken cancellationToken)
    {
        if (job.Definition.ChangedFiles is not { } probe)
        {
            return;
        }

        job.SetChangedFiles(await probe.CollectAsync(cancellationToken));
    }

    private RunningJob[] EvictFinishedJobs()
    {
        int excess = this._jobs.Count - this._settings.MaxRetainedJobs + 1;

        if (excess <= 0)
        {
            return [];
        }

        // Oldest finished jobs go first; a running job is never evicted. Their log files stay on disk
        RunningJob[] evicted =
        [
            .. this._jobs.Values.Where(static job => !job.IsRunning).OrderBy(static job => job.Sequence).Take(excess),
        ];

        foreach (RunningJob job in evicted)
        {
            _ = this._jobs.Remove(job.Id);
        }

        return evicted;
    }

    private static void CreateLogDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(directory);

            return;
        }

        _ = Directory.CreateDirectory(
            path: directory,
            unixCreateMode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
    }
}
