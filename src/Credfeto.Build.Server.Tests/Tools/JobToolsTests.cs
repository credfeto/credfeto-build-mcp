using System;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
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
}
