using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Services;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Server.Tests.Tools;

public sealed class JobResultWaitTests : TestBase, IDisposable
{
    private const string RUNNING = "\"state\":\"running\"";
    private const string SUCCEEDED = "\"state\":\"succeeded\"";

    private static readonly TimeSpan JobTimeout = TimeSpan.FromHours(1);

    private readonly GatedProcessLauncher _launcher = new();
    private readonly TemporaryDirectory _logs = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose()
    {
        this._logs.Dispose();
    }

    [Fact]
    public static void TheWaitIsAWholeNumberFromZeroToSixHundredDefaultingTo240()
    {
        JobResultTool tool = new(new RecordingJobRunner());

        Assert.Equal(
            expected: """{"type":"object","properties":{"id":{"type":"string","description":"The job id returned by a tool that starts a job","pattern":"^[0-9a-f]{32}$"},"wait_seconds":{"type":"integer","description":"How long to wait for a running job to finish, in seconds (0 to 600); 0 answers at once with the job as it is now","minimum":0,"maximum":600,"default":240}},"required":["id"],"additionalProperties":false}""",
            actual: tool.ProtocolTool.InputSchema.GetRawText()
        );
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("601")]
    [InlineData("1.5")]
    [InlineData("\"10\"")]
    [InlineData("true")]
    public async Task AWaitOutsideTheBoundsIsAnArgumentErrorAsync(string waitJson)
    {
        CallToolResult result = await new JobResultTool(new RecordingJobRunner()).CallAsync(
            arguments: Arguments.JobWait(id: RecordingJobRunner.JOB_ID, waitJson: waitJson),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result.IsError, userMessage: "an invalid wait is refused");
        Assert.Equal(
            expected: "Invalid value for wait_seconds; expected a whole number from 0 to 600",
            actual: Arguments.Answer(result)
        );
    }

    [Fact]
    public async Task AWaitOfZeroAnswersAtOnceAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a");

        Task<CallToolResult> call = this.CallAsync(runner: runner, id: id, waitJson: "0", progress: ToolProgress.None);

        Assert.True(condition: call.IsCompleted, userMessage: "no wait at all");
        AssertState(result: await call, state: RUNNING);
        this._launcher.Release("a");
    }

    [Fact]
    public async Task AFinishedJobIsNotWaitedForAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a");
        this._launcher.Release("a");
        _ = await runner.WaitForCompletionAsync(
            id: id,
            maxWait: Timeout.InfiniteTimeSpan,
            cancellationToken: this.CancellationToken()
        );

        Task<CallToolResult> call = this.CallAsync(
            runner: runner,
            id: id,
            waitJson: "600",
            progress: ToolProgress.None
        );

        Assert.True(condition: call.IsCompleted, userMessage: "a finished job answers at once");
        AssertState(result: await call, state: SUCCEEDED);
    }

    [Fact]
    public async Task TheDefaultWaitReturnsAsSoonAsTheJobFinishesAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a");

        Task<CallToolResult> call = new JobResultTool(runner: runner, timeProvider: this._time)
            .CallAsync(arguments: Arguments.Text(name: "id", value: id), cancellationToken: this.CancellationToken())
            .AsTask();
        Assert.False(condition: call.IsCompleted, userMessage: "the call waits for the running job");

        this._launcher.Release("a");

        AssertState(result: await call, state: SUCCEEDED);
    }

    [Fact]
    public async Task AWaitThatRunsOutReturnsTheRunningJobAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a");

        Task<CallToolResult> call = this.CallAsync(runner: runner, id: id, waitJson: "30", progress: ToolProgress.None);
        this._time.Advance(TimeSpan.FromSeconds(30));

        AssertState(result: await call, state: RUNNING);
        this._launcher.Release("a");
    }

    [Fact]
    public async Task CancellingTheCallEndsOnlyTheWaitAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a");
        using CancellationTokenSource cancellation = new();

        Task<CallToolResult> call = new JobResultTool(runner: runner, timeProvider: this._time)
            .CallAsync(arguments: Arguments.Text(name: "id", value: id), cancellationToken: cancellation.Token)
            .AsTask();
        await cancellation.CancelAsync();

        bool cancelled = false;

        try
        {
            _ = await call;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.True(condition: cancelled, userMessage: "the cancelled wait throws");
        Assert.Equal(expected: JobState.Running, actual: runner.FindStatus(id)?.State);

        this._launcher.Release("a");
        bool finished = await runner.WaitForCompletionAsync(
            id: id,
            maxWait: Timeout.InfiniteTimeSpan,
            cancellationToken: this.CancellationToken()
        );
        Assert.True(condition: finished, userMessage: "the job carried on and finished");
        Assert.Equal(expected: JobState.Succeeded, actual: runner.FindStatus(id)?.State);
    }

    [Fact]
    public async Task ProgressIsReportedOnEachStepChangeAndOnTheTickAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a", "b");
        RecordingProgress progress = new(enabled: true);

        Task<CallToolResult> call = this.CallAsync(runner: runner, id: id, waitJson: "600", progress: progress);
        Assert.Equal(expected: "0 of 2 steps done; running a", actual: Assert.Single(progress.Reports).Message);

        this._launcher.Release("a");
        await Waiting.ForAsync(
            condition: () => progress.Reports.Count == 2,
            what: "the step change to be reported",
            cancellationToken: this.CancellationToken()
        );
        Assert.Equal(expected: "1 of 2 steps done; running b", actual: progress.Reports[1].Message);

        await this.AdvanceUntilAsync(() => progress.Reports.Count == 3);
        Assert.Equal(expected: "1 of 2 steps done; running b", actual: progress.Reports[2].Message);

        this._launcher.Release("b");
        AssertState(result: await call, state: SUCCEEDED);

        IReadOnlyList<double> values = [.. progress.Reports.Select(static report => report.Progress)];
        Assert.True(
            condition: values.Zip(values.Skip(1)).All(static pair => pair.Second > pair.First),
            userMessage: "each notification's progress is larger than the last: "
                + string.Join(separator: ", ", values: values)
        );
    }

    [Fact]
    public async Task ShutdownEndsAWaitWhileTheJobIsStillStoppingAsync()
    {
        GatedProcessLauncher slowToStop = new(ignoresCancellation: true);
        JobRunner runner = this.CreateRunner(slowToStop);
        JobStatus started = await runner.StartAsync(GatedProcessLauncher.Job(timeout: JobTimeout, "a"));
        await slowToStop.WaitForStartAsync(step: "a", cancellationToken: this.CancellationToken());
        RecordingProgress progress = new(enabled: true);

        Task<CallToolResult> call = this.CallAsync(runner: runner, id: started.Id, waitJson: "600", progress: progress);
        Task disposing = runner.DisposeAsync().AsTask();

        AssertState(result: await call, state: RUNNING);
        Assert.Single(progress.Reports);

        slowToStop.Release("a");
        await disposing;
    }

    [Fact]
    public async Task WithoutAProgressTokenNothingIsReportedAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        string id = await this.StartAsync(runner, "a", "b");
        RecordingProgress progress = new(enabled: false);

        Task<CallToolResult> call = this.CallAsync(runner: runner, id: id, waitJson: "600", progress: progress);
        this._launcher.Release("a");
        this._launcher.Release("b");

        AssertState(result: await call, state: SUCCEEDED);
        Assert.Empty(progress.Reports);
    }

    private static void AssertState(CallToolResult result, string state)
    {
        Assert.False(condition: result.IsError, userMessage: Arguments.Answer(result));
        Assert.Contains(
            expectedSubstring: state,
            actualString: Arguments.Answer(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    private Task<CallToolResult> CallAsync(JobRunner runner, string id, string waitJson, IToolProgress progress)
    {
        return new JobResultTool(runner: runner, timeProvider: this._time)
            .CallAsync(
                arguments: Arguments.JobWait(id: id, waitJson: waitJson),
                progress: progress,
                cancellationToken: this.CancellationToken()
            )
            .AsTask();
    }

    // The tick's timer is set once the waiter has reported, on another thread, so time moves on until it has fired
    private async ValueTask AdvanceUntilAsync(Func<bool> condition)
    {
        int attempts = 0;

        while (!condition())
        {
            Assert.True(condition: ++attempts <= 20, userMessage: "the tick never fired");
            this._time.Advance(JobResultTool.ProgressInterval);
            await Task.Delay(delay: TimeSpan.FromMilliseconds(25), cancellationToken: this.CancellationToken());
        }
    }

    private async ValueTask<string> StartAsync(JobRunner runner, params string[] steps)
    {
        JobStatus started = await runner.StartAsync(GatedProcessLauncher.Job(timeout: JobTimeout, steps: steps));
        await this._launcher.WaitForStartAsync(step: steps[0], cancellationToken: this.CancellationToken());

        return started.Id;
    }

    private JobRunner CreateRunner(IProcessLauncher? launcher = null)
    {
        return new(
            launcher: launcher ?? this._launcher,
            masker: new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal)),
            settings: JobRunnerSettings.Create(this._logs.Path),
            timeProvider: this._time
        );
    }
}
