using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Server.Tools;

public sealed class JobResultTool : BuildTool
{
    private const string WAIT_SECONDS_DEFAULT = "240";
    private const int WAIT_SECONDS_MAXIMUM = 600;

    // Often enough that a client does not think the call has hung, rarely enough not to flood it
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(15);

    private readonly SchemaProperty _id;
    private readonly IJobRunner _runner;
    private readonly TimeProvider _timeProvider;
    private readonly SchemaProperty _waitSeconds;

    public JobResultTool(IJobRunner runner)
        : this(runner: runner, timeProvider: TimeProvider.System) { }

    public JobResultTool(IJobRunner runner, TimeProvider timeProvider)
        : this(
            runner: runner,
            timeProvider: timeProvider,
            id: JobIdProperty.Create(),
            waitSeconds: WaitSecondsProperty()
        ) { }

    private JobResultTool(IJobRunner runner, TimeProvider timeProvider, SchemaProperty id, SchemaProperty waitSeconds)
        : base(
            name: JobToolNames.JOB_RESULT,
            title: "Job result",
            description: "Waits up to wait_seconds (default "
                + WAIT_SECONDS_DEFAULT
                + ") for the job to finish, then returns its outcome: state, per-step exit codes, test counts and failing tests, coverage figures, the masked last lines of output and the full log path. If it is still running, state is running and the figures are partial; call it again",
            schema: [id, waitSeconds],
            readOnly: true
        )
    {
        this._runner = runner;
        this._timeProvider = timeProvider;
        this._id = id;
        this._waitSeconds = waitSeconds;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        string id = arguments.GetJobId(this._id);
        TimeSpan maxWait = TimeSpan.FromSeconds(arguments.GetInteger(this._waitSeconds));

        JobStatus status = this._runner.FindStatus(id) ?? throw new JobNotFoundException();

        if (maxWait > TimeSpan.Zero && status.State == JobState.Running)
        {
            await this.WaitAsync(
                status: status,
                maxWait: maxWait,
                progress: progress,
                cancellationToken: cancellationToken
            );
        }

        JobResult result = this._runner.FindResult(id) ?? throw new JobNotFoundException();

        return JsonSerializer.Serialize(value: result, jsonTypeInfo: CoreJsonContext.Default.JobResult);
    }

    private async ValueTask WaitAsync(
        JobStatus status,
        TimeSpan maxWait,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        long started = this._timeProvider.GetTimestamp();

        while (true)
        {
            TimeSpan remaining = maxWait - this._timeProvider.GetElapsedTime(started);

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            await ReportAsync(status: status, progress: progress, cancellationToken: cancellationToken);

            bool finished = await this._runner.WaitForChangeAsync(
                id: status.Id,
                stepsCompleted: status.StepsCompleted,
                maxWait: remaining < ProgressInterval ? remaining : ProgressInterval,
                cancellationToken: cancellationToken
            );

            if (finished || this._runner.FindStatus(status.Id) is not { State: JobState.Running } current)
            {
                return;
            }

            status = current;
        }
    }

    // Only the step name and counts, never process output, so nothing unmasked reaches the client early
    private static ValueTask ReportAsync(JobStatus status, IToolProgress progress, CancellationToken cancellationToken)
    {
        return progress.ReportAsync(
            progress: status.StepsCompleted,
            total: status.StepCount,
            message: status.CurrentStep is null ? "running" : "running " + status.CurrentStep,
            cancellationToken: cancellationToken
        );
    }

    private static SchemaProperty WaitSecondsProperty()
    {
        return new(
            Name: "wait_seconds",
            Description: "How many seconds to wait for the job to finish before answering; 0 answers at once",
            Values: null,
            Pattern: null,
            DefaultValue: WAIT_SECONDS_DEFAULT,
            Required: false
        )
        {
            WholeNumber = true,
            Minimum = 0,
            Maximum = WAIT_SECONDS_MAXIMUM,
        };
    }
}
