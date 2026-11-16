using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Server.Tests.Tools;

public sealed class JobToolsTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();
    private readonly RecordingJobRunner _runner = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task JobStatusReturnsTheRunnersStatusAsync()
    {
        this._runner.Know(
            new(
                Id: RecordingJobRunner.JOB_ID,
                Kind: "build",
                State: JobState.Succeeded,
                DurationSeconds: 1.5,
                StepsCompleted: 1,
                StepCount: 1,
                CurrentStep: null,
                LogPath: "/logs/job.log"
            )
        );

        CallToolResult result = await new JobStatusTool(this._runner).CallAsync(
            arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result.IsError, userMessage: Arguments.Answer(result));
        Assert.Contains(
            expectedSubstring: "\"state\":\"succeeded\"",
            actualString: Arguments.Answer(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AnUnknownJobIsAToolErrorForStatusAndResultAsync()
    {
        CallToolResult status = await new JobStatusTool(this._runner).CallAsync(
            arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
            cancellationToken: this.CancellationToken()
        );
        CallToolResult result = await new JobResultTool(this._runner).CallAsync(
            arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: status.IsError, userMessage: "job_status of an unknown job");
        Assert.True(condition: result.IsError, userMessage: "job_result of an unknown job");
        Assert.Equal(expected: Arguments.Answer(status), actual: Arguments.Answer(result));
    }

    [Fact]
    public static void TheJobToolsAreNamedAndTakeARequiredId()
    {
        JobStatusTool status = new(new RecordingJobRunner());
        JobResultTool result = new(new RecordingJobRunner());

        Assert.Equal(expected: JobToolNames.JOB_STATUS, actual: status.ProtocolTool.Name);
        Assert.Equal(expected: JobToolNames.JOB_RESULT, actual: result.ProtocolTool.Name);
        Assert.Equal(expected: "id", actual: status.ProtocolTool.InputSchema.GetProperty("required")[0].GetString());
        Assert.Equal(expected: "id", actual: result.ProtocolTool.InputSchema.GetProperty("required")[0].GetString());
    }

    [Fact]
    public static void JobResultTakesAnOptionalWaitOfUpToTenMinutes()
    {
        JobResultTool result = new(new RecordingJobRunner());

        Assert.Equal(
            expected: """{"type":"integer","description":"How many seconds to wait for the job to finish before answering; 0 answers at once","minimum":0,"maximum":600,"default":240}""",
            actual: result.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("wait_seconds").GetRawText()
        );
        Assert.Equal(expected: 1, actual: result.ProtocolTool.InputSchema.GetProperty("required").GetArrayLength());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("601")]
    [InlineData("1.5")]
    [InlineData("\"10\"")]
    public async Task AnInvalidWaitIsAToolErrorAsync(string waitSeconds)
    {
        this._runner.Know(Status(state: JobState.Running, stepsCompleted: 0, currentStep: "build"));
        this._runner.KnowResult(Result(JobState.Running));

        CallToolResult result = await new JobResultTool(this._runner).CallAsync(
            arguments: WaitArguments(waitSeconds),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result.IsError, userMessage: Arguments.Answer(result));
        Assert.Equal(
            expected: "Invalid value for wait_seconds; expected a whole number from 0 to 600",
            actual: Arguments.Answer(result)
        );
    }

    [Fact]
    public async Task AWaitOfNothingAnswersAtOnceWithTheRunningJobAsync()
    {
        this._runner.Know(Status(state: JobState.Running, stepsCompleted: 0, currentStep: "build"));
        this._runner.KnowResult(Result(JobState.Running));
        RecordingToolProgress progress = new();

        CallToolResult result = await new JobResultTool(this._runner).CallAsync(
            arguments: WaitArguments("0"),
            progress: progress,
            cancellationToken: this.CancellationToken()
        );

        AssertState(result: result, state: "running");
        Assert.Empty(progress.Reports);
    }

    [Fact]
    public async Task AFinishedJobAnswersAtOnceAsync()
    {
        this._runner.Know(Status(state: JobState.Succeeded, stepsCompleted: 2, currentStep: null));
        this._runner.KnowResult(Result(JobState.Succeeded));
        RecordingToolProgress progress = new();

        CallToolResult result = await new JobResultTool(this._runner).CallAsync(
            arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
            progress: progress,
            cancellationToken: this.CancellationToken()
        );

        AssertState(result: result, state: "succeeded");
        Assert.Empty(progress.Reports);
    }

    [Fact]
    public async Task AJobThatFinishesWhileWaitedOnIsAnsweredWithItsOutcomeAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        this._runner.Know(Status(state: JobState.Running, stepsCompleted: 0, currentStep: "build"));
        this._runner.KnowResult(Result(JobState.Running));
        RecordingToolProgress progress = new();

        Task<CallToolResult> call = new JobResultTool(this._runner)
            .CallAsync(
                arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
                progress: progress,
                cancellationToken: cancellationToken
            )
            .AsTask();

        Assert.Equal(
            expected: TimeSpan.FromSeconds(15),
            actual: await this._runner.WaitUntilWaitingAsync(cancellationToken)
        );
        this._runner.Know(Status(state: JobState.Running, stepsCompleted: 1, currentStep: "test"));

        _ = await this._runner.WaitUntilWaitingAsync(cancellationToken);
        this._runner.KnowResult(Result(JobState.Succeeded));
        this._runner.Know(Status(state: JobState.Succeeded, stepsCompleted: 2, currentStep: null));

        AssertState(result: await call, state: "succeeded");
        Assert.Equal(
            expected: ["0/2 running build", "1/2 running test"],
            actual: progress.Reports,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task AJobWaitedOnWithoutAProgressTokenIsAnsweredWithItsOutcomeAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        this._runner.Know(Status(state: JobState.Running, stepsCompleted: 0, currentStep: "build"));
        this._runner.KnowResult(Result(JobState.Running));

        Task<CallToolResult> call = new JobResultTool(this._runner)
            .CallAsync(
                arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
                cancellationToken: cancellationToken
            )
            .AsTask();

        _ = await this._runner.WaitUntilWaitingAsync(cancellationToken);
        this._runner.KnowResult(Result(JobState.Succeeded));
        this._runner.Know(Status(state: JobState.Succeeded, stepsCompleted: 2, currentStep: null));

        AssertState(result: await call, state: "succeeded");
    }

    [Fact]
    public async Task AJobWhoseResultHasGoneIsAToolErrorAsync()
    {
        // The status was found but the job went before its result was read, which reads the same as an unknown job
        this._runner.Know(Status(state: JobState.Succeeded, stepsCompleted: 2, currentStep: null));

        CallToolResult status = await new JobStatusTool(new RecordingJobRunner()).CallAsync(
            arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
            cancellationToken: this.CancellationToken()
        );
        CallToolResult result = await new JobResultTool(this._runner).CallAsync(
            arguments: Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result.IsError, userMessage: Arguments.Answer(result));
        Assert.Equal(expected: Arguments.Answer(status), actual: Arguments.Answer(result));
    }

    [Fact]
    public async Task AWaitThatRunsOutAnswersWithTheRunningJobAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        FakeTimeProvider timeProvider = new();
        RecordingJobRunner runner = new(timeProvider);
        runner.Know(Status(state: JobState.Running, stepsCompleted: 0, currentStep: null));
        runner.KnowResult(Result(JobState.Running));
        RecordingToolProgress progress = new();

        Task<CallToolResult> call = new JobResultTool(runner: runner, timeProvider: timeProvider)
            .CallAsync(arguments: WaitArguments("20"), progress: progress, cancellationToken: cancellationToken)
            .AsTask();

        Assert.Equal(expected: TimeSpan.FromSeconds(15), actual: await runner.WaitUntilWaitingAsync(cancellationToken));
        timeProvider.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(expected: TimeSpan.FromSeconds(5), actual: await runner.WaitUntilWaitingAsync(cancellationToken));
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        AssertState(result: await call, state: "running");
        Assert.Equal(
            expected: ["0/2 running", "0/2 running"],
            actual: progress.Reports,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task JobStarterStartsTheJobWithTheRepositorysTimeoutAsync()
    {
        string started = await JobStarter.StartAsync(
            runner: this._runner,
            repositoryRoot: this._repository.Path,
            timeout: TimeSpan.FromMinutes(7),
            kind: "build",
            steps: [],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: this.CancellationToken()
        );

        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: "build", actual: definition.Kind);
        Assert.Equal(expected: TimeSpan.FromMinutes(7), actual: definition.Timeout);
        Assert.False(condition: definition.ContinueOnFailure, userMessage: "no test parser, so a failure ends the job");
        Assert.Null(definition.HeadSha);
        Assert.Null(definition.CoverageReportPath);

        using JsonDocument document = JsonDocument.Parse(started);
        Assert.Equal(expected: RecordingJobRunner.JOB_ID, actual: document.RootElement.GetProperty("id").GetString());
    }

    private static Dictionary<string, JsonElement> WaitArguments(string waitSeconds)
    {
        Dictionary<string, JsonElement> arguments = Arguments.Text(name: "id", value: RecordingJobRunner.JOB_ID);

        using JsonDocument document = JsonDocument.Parse(waitSeconds);
        arguments["wait_seconds"] = document.RootElement.Clone();

        return arguments;
    }

    private static JobStatus Status(JobState state, int stepsCompleted, string? currentStep)
    {
        return new(
            Id: RecordingJobRunner.JOB_ID,
            Kind: "buildtest",
            State: state,
            DurationSeconds: 1,
            StepsCompleted: stepsCompleted,
            StepCount: 2,
            CurrentStep: currentStep,
            LogPath: "/logs/job.log"
        );
    }

    private static JobResult Result(JobState state)
    {
        return new(
            Id: RecordingJobRunner.JOB_ID,
            Kind: "buildtest",
            State: state,
            DurationSeconds: 1,
            LogPath: "/logs/job.log",
            Steps: [],
            Tests: null,
            FailedTests: [],
            OutputTail: [],
            Coverage: null,
            CoverageMarkdown: null,
            CoverageErrors: [],
            HeadSha: null,
            Message: null,
            ChangedFiles: null,
            CoverageReportPath: null
        );
    }

    private static void AssertState(CallToolResult result, string state)
    {
        Assert.False(condition: result.IsError, userMessage: Arguments.Answer(result));
        Assert.Contains(
            expectedSubstring: "\"state\":\"" + state + "\"",
            actualString: Arguments.Answer(result),
            comparisonType: StringComparison.Ordinal
        );
    }
}
