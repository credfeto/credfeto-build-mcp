using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Tools;

public sealed class ToolCallTests : TestBase, IDisposable
{
    private const string JOB_ID = RecordingJobRunner.JOB_ID;

    private readonly TemporaryDirectory _repository = new();
    private readonly RecordingJobRunner _runner = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task CoverageOfAllRunsEveryUnitTestProjectAndCollectsTheirFilesAsync()
    {
        string stale = await this._repository.WriteAsync(
            relative: "coverage/Example.Core.coverage.cobertura.xml",
            content: "old",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.CallAsync(name: "coverage", arguments: [with(StringComparer.Ordinal)]);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Contains(expectedSubstring: JOB_ID, actualString: Text(result), comparisonType: StringComparison.Ordinal);

        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: "coverage", actual: definition.Kind);
        Assert.Equal(
            expected: ["coverage Example.Core.Tests", "coverage Example.Api.Tests"],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected:
            [
                this._repository.Combine("coverage/Example.Core.coverage.cobertura.xml"),
                this._repository.Combine("coverage/Example.Api.coverage.cobertura.xml"),
            ],
            actual: definition.CoverageFiles,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: definition.ContinueOnFailure, userMessage: "every project is measured");
        Assert.False(condition: File.Exists(stale), userMessage: "a stale coverage file is removed before the run");
    }

    [Fact]
    public async Task CoverageOfOneProjectRunsOnlyThatProjectAsync()
    {
        CallToolResult result = await this.CallAsync(name: "coverage", arguments: Arguments(name: "project", value: "Example.Api.Tests"));

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(expected: "coverage Example.Api.Tests", actual: Assert.Single(Assert.Single(this._runner.Started).Steps).Name);
    }

    [Fact]
    public async Task TestOfOneProjectRunsOnlyThatProjectAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments(name: "scope", value: "project");
        arguments["project"] = JsonSerializer.SerializeToElement(value: "Example.Core.Tests", jsonTypeInfo: TestJsonContext.Default.String);

        CallToolResult result = await this.CallAsync(name: "test", arguments: arguments);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: "test Example.Core.Tests", actual: Assert.Single(definition.Steps).Name);
        Assert.NotNull(definition.TestParser);
        Assert.Empty(definition.CoverageFiles);
    }

    [Fact]
    public async Task TestWithNoArgumentsIsOneSolutionWideRunAsync()
    {
        CallToolResult result = await this.CallAsync(name: "test", arguments: [with(StringComparer.Ordinal)]);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep step = Assert.Single(Assert.Single(this._runner.Started).Steps);
        Assert.Equal(expected: ["test", "--solution", "Example.slnx", "-c", "Release"], actual: step.Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: this._repository.Combine("src"), actual: step.WorkingDirectory);
    }

    [Fact]
    public async Task BuildIsTheWholeSolutionAsync()
    {
        CallToolResult result = await this.CallAsync(name: "build", arguments: [with(StringComparer.Ordinal)]);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep step = Assert.Single(Assert.Single(this._runner.Started).Steps);
        Assert.Equal(expected: ["build", "Example.slnx", "-c", "Release"], actual: step.Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: this._repository.Combine("src"), actual: step.WorkingDirectory);
    }

    [Theory]
    [InlineData("test", "scope", "everything")]
    [InlineData("test", "project", "Example.Core.Tests")]
    [InlineData("coverage", "project", "../../etc/passwd")]
    [InlineData("coverage", "project", "Example.Integration.Tests")]
    [InlineData("build", "configuration", "Debug")]
    [InlineData("job_status", "id", "not-an-id")]
    public async Task InvalidArgumentsAreRefusedWithoutStartingAnythingAsync(string tool, string name, string value)
    {
        CallToolResult result = await this.CallAsync(name: tool, arguments: Arguments(name: name, value: value));

        Assert.True(condition: result.IsError, userMessage: tool + " accepted " + name);
        Assert.DoesNotContain(expectedSubstring: "passwd", actualString: Text(result), comparisonType: StringComparison.Ordinal);
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task NonStringArgumentIsRefusedAsync()
    {
        Dictionary<string, JsonElement> arguments = new(StringComparer.Ordinal)
        {
            ["project"] = JsonSerializer.SerializeToElement(value: 3, jsonTypeInfo: TestJsonContext.Default.Int32),
        };

        CallToolResult result = await this.CallAsync(name: "coverage", arguments: arguments);

        Assert.True(condition: result.IsError, userMessage: "a number is not a project");
    }

    [Fact]
    public async Task BusyRunnerIsReportedAsAToolErrorAsync()
    {
        this._runner.Busy = true;

        CallToolResult result = await this.CallAsync(name: "build", arguments: [with(StringComparer.Ordinal)]);

        Assert.True(condition: result.IsError, userMessage: "busy is an error");
        Assert.Contains(expectedSubstring: "still running", actualString: Text(result), comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownJobIsReportedAsAToolErrorAsync()
    {
        CallToolResult result = await this.CallAsync(name: "job_result", arguments: Arguments(name: "id", value: JOB_ID));

        Assert.True(condition: result.IsError, userMessage: "unknown job is an error");
    }

    [Fact]
    public async Task JobStatusReturnsTheRunnersStatusAsync()
    {
        this._runner.Know(
            new(Id: JOB_ID, Kind: "build", State: JobState.Succeeded, DurationSeconds: 12.5, StepsCompleted: 1, StepCount: 1, CurrentStep: null, LogPath: "/logs/job.log")
        );

        CallToolResult result = await this.CallAsync(name: "job_status", arguments: Arguments(name: "id", value: JOB_ID));

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Contains(expectedSubstring: "\"state\":\"succeeded\"", actualString: Text(result), comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectListsTheSolutionAndTestProjectsAsync()
    {
        CallToolResult result = await this.CallAsync(name: "detect", arguments: [with(StringComparer.Ordinal)]);

        using JsonDocument document = JsonDocument.Parse(Text(result));
        Assert.Equal(expected: "src/Example.slnx", actual: document.RootElement.GetProperty("solution").GetString());
        Assert.Equal(expected: 2, actual: document.RootElement.GetProperty("unitTestProjects").GetArrayLength());
        Assert.Equal(expected: "Example.Integration.Tests", actual: document.RootElement.GetProperty("integrationTestProjects")[0].GetString());
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments)
    {
        IReadOnlyList<McpServerTool> tools = TestContexts.Tools(
            runner: this._runner,
            detection: TestRepositories.Detected(
                TestRepositories.Create(root: this._repository.Path, TestRepositories.CoreTests, TestRepositories.ApiTests)
            )
        );

        return TestContexts.Find(tools: tools, name: name).CallAsync(arguments: arguments, cancellationToken: this.CancellationToken());
    }

    private static Dictionary<string, JsonElement> Arguments(string name, string value)
    {
        return new(StringComparer.Ordinal)
        {
            [name] = JsonSerializer.SerializeToElement(value: value, jsonTypeInfo: TestJsonContext.Default.String),
        };
    }

    private static string Text(CallToolResult result)
    {
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }
}
