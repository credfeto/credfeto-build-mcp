using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Server.Tools;

public sealed class JobResultTool : BuildTool
{
    public const string WAIT_SECONDS = "wait_seconds";
    public const int DEFAULT_WAIT_SECONDS = 240;
    public const int MAX_WAIT_SECONDS = 600;

    // A client that asked for progress hears at least this often, so an idle timeout never ends a long wait
    public static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(15);

    private readonly SchemaProperty _id;
    private readonly IJobRunner _runner;
    private readonly TimeProvider _timeProvider;
    private readonly SchemaProperty _wait;

    public JobResultTool(IJobRunner runner)
        : this(runner: runner, timeProvider: TimeProvider.System) { }

    public JobResultTool(IJobRunner runner, TimeProvider timeProvider)
        : this(runner: runner, timeProvider: timeProvider, id: JobIdProperty.Create(), wait: WaitProperty()) { }

    private JobResultTool(IJobRunner runner, TimeProvider timeProvider, SchemaProperty id, SchemaProperty wait)
        : base(
            name: JobToolNames.JOB_RESULT,
            title: "Job result",
            description: "Waits up to wait_seconds (default 240) for the job to finish, returning as soon as it does, then returns its outcome: state, per-step exit codes, test counts and failing tests, coverage figures, the masked last lines of output and the full log path. If the job is still running when the wait ends, state is running and the figures are partial; call it again. While it waits it sends notifications/progress when the request has a progress token; cancelling the request ends only the wait, never the job",
            schema: [id, wait],
            readOnly: true
        )
    {
        this._runner = runner;
        this._timeProvider = timeProvider;
        this._id = id;
        this._wait = wait;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            arguments: arguments,
            progress: ToolProgress.None,
            cancellationToken: cancellationToken
        );
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        string id = arguments.GetJobId(this._id);
        TimeSpan wait = TimeSpan.FromSeconds(arguments.GetInteger(this._wait));
        JobWatch watch = this._runner.Watch(id) ?? throw new JobNotFoundException();

        if (watch.Status.State == JobState.Running && wait > TimeSpan.Zero)
        {
            await this.WaitAsync(watch: watch, wait: wait, progress: progress, cancellationToken: cancellationToken);
        }

        JobResult result = this._runner.FindResult(id) ?? throw new JobNotFoundException();

        return JsonSerializer.Serialize(value: result, jsonTypeInfo: CoreJsonContext.Default.JobResult);
    }

    private async ValueTask WaitAsync(
        JobWatch watch,
        TimeSpan wait,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (!progress.Enabled)
        {
            _ = await this._runner.WaitForCompletionAsync(
                id: watch.Status.Id,
                maxWait: wait,
                cancellationToken: cancellationToken
            );

            return;
        }

        long started = this._timeProvider.GetTimestamp();
        double reported = 0;

        for (
            JobWatch? current = watch;
            current is { Status.State: JobState.Running };
            current = this._runner.Watch(watch.Status.Id)
        )
        {
            TimeSpan remaining = wait - this._timeProvider.GetElapsedTime(started);

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // MCP wants each notification's progress to be larger than the last; the job's running time is, and the
            // steps go in the message
            reported = Math.Max(val1: current.Status.DurationSeconds, val2: reported + 0.1);
            await progress.ReportAsync(
                progress: reported,
                message: Describe(current.Status),
                cancellationToken: cancellationToken
            );

            JobWaitOutcome outcome = await this._runner.WaitForChangeAsync(
                watch: current,
                maxWait: remaining < ProgressInterval ? remaining : ProgressInterval,
                cancellationToken: cancellationToken
            );

            if (outcome == JobWaitOutcome.ShuttingDown)
            {
                return;
            }
        }
    }

    // Fixed text and the server's own step names, never process output
    private static string Describe(JobStatus status)
    {
        string steps =
            status.StepsCompleted.ToString(CultureInfo.InvariantCulture)
            + " of "
            + status.StepCount.ToString(CultureInfo.InvariantCulture)
            + " steps done";

        return status.CurrentStep is { } step && status.StepsCompleted < status.StepCount
            ? steps + "; running " + step
            : steps;
    }

    private static SchemaProperty WaitProperty()
    {
        return new(
            Name: WAIT_SECONDS,
            Description: "How long to wait for a running job to finish, in seconds (0 to 600); 0 answers at once with the job as it is now",
            Values: null,
            Pattern: null,
            DefaultValue: DEFAULT_WAIT_SECONDS.ToString(CultureInfo.InvariantCulture),
            Required: false
        )
        {
            WholeNumber = true,
            Minimum = 0,
            Maximum = MAX_WAIT_SECONDS,
        };
    }
}
