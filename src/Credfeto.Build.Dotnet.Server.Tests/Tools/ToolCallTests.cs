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
        Assert.Contains(
            expectedSubstring: JOB_ID,
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );

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
        CallToolResult result = await this.CallAsync(
            name: "coverage",
            arguments: Arguments(name: "project", value: "Example.Api.Tests")
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: "coverage Example.Api.Tests",
            actual: Assert.Single(Assert.Single(this._runner.Started).Steps).Name
        );
    }

    [Theory]
    [InlineData("Example.Core.Tests")]
    [InlineData("Example.Integration.Tests")]
    [InlineData("Example.Benchmark.Tests")]
    public async Task TestOfOneProjectBuildsThenRunsOnlyThatProjectAsync(string project)
    {
        Dictionary<string, JsonElement> arguments = Arguments(name: "scope", value: "project");
        arguments["project"] = JsonSerializer.SerializeToElement(
            value: project,
            jsonTypeInfo: TestJsonContext.Default.String
        );

        CallToolResult result = await this.CallAsync(name: "test", arguments: arguments);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["restore", "build " + project, "test " + project],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.DoesNotContain(
            expected: "--filter-not-namespace",
            collection: definition.Steps[2].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.NotNull(definition.TestParser);
        Assert.Empty(definition.CoverageFiles);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestOfAllRestoresBuildsThenRunsTheFilteredSolutionAsync(bool explicitScope)
    {
        CallToolResult result = await this.CallAsync(
            name: "test",
            arguments: explicitScope ? Arguments(name: "scope", value: "all") : [with(StringComparer.Ordinal)]
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["restore", "build", "test"],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.True(
            condition: definition.Steps[0].StopJobOnFailure && definition.Steps[1].StopJobOnFailure,
            userMessage: "restore and build stop the job"
        );

        ProcessStep test = definition.Steps[2];
        Assert.Equal(
            expected: ["test", "--solution", "Example.slnx"],
            actual: test.Arguments.Take(3),
            comparer: StringComparer.Ordinal
        );
        Assert.Contains(expected: "--no-build", collection: test.Arguments, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "*.Benchmark.Tests", collection: test.Arguments, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "*.Integration.Tests", collection: test.Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: this._repository.Combine("src"), actual: test.WorkingDirectory);
    }

    [Fact]
    public async Task BuildRestoresThenBuildsTheWholeSolutionAsync()
    {
        CallToolResult result = await this.CallAsync(name: "build", arguments: [with(StringComparer.Ordinal)]);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["restore", "build"],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );

        ProcessStep step = definition.Steps[1];
        Assert.Equal(
            expected: ["build", "Example.slnx", "-c", "Release", "--no-restore"],
            actual: step.Arguments.Take(5),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "-nodeReuse:False", actual: step.Arguments[^1]);
        Assert.Equal(expected: this._repository.Combine("src"), actual: step.WorkingDirectory);
    }

    [Theory]
    [InlineData("test", "scope", "everything")]
    [InlineData("test", "project", "Example.Core.Tests")]
    [InlineData("coverage", "project", "../../etc/passwd")]
    [InlineData("coverage", "project", "Example.Integration.Tests")]
    [InlineData("coverage", "project", "Example.Benchmark.Tests")]
    [InlineData("build", "configuration", "Debug")]
    [InlineData("job_status", "id", "not-an-id")]
    public async Task InvalidArgumentsAreRefusedWithoutStartingAnythingAsync(string tool, string name, string value)
    {
        CallToolResult result = await this.CallAsync(name: tool, arguments: Arguments(name: name, value: value));

        Assert.True(condition: result.IsError, userMessage: tool + " accepted " + name);
        Assert.DoesNotContain(
            expectedSubstring: "passwd",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
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
        Assert.Contains(
            expectedSubstring: "still running",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task UnknownJobIsReportedAsAToolErrorAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: "job_result",
            arguments: Arguments(name: "id", value: JOB_ID)
        );

        Assert.True(condition: result.IsError, userMessage: "unknown job is an error");
    }

    [Fact]
    public async Task JobStatusReturnsTheRunnersStatusAsync()
    {
        this._runner.Know(
            new(
                Id: JOB_ID,
                Kind: "build",
                State: JobState.Succeeded,
                DurationSeconds: 12.5,
                StepsCompleted: 1,
                StepCount: 1,
                CurrentStep: null,
                LogPath: "/logs/job.log"
            )
        );

        CallToolResult result = await this.CallAsync(
            name: "job_status",
            arguments: Arguments(name: "id", value: JOB_ID)
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Contains(
            expectedSubstring: "\"state\":\"succeeded\"",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task DetectListsTheSolutionAndTestProjectsAsync()
    {
        CallToolResult result = await this.CallAsync(name: "detect", arguments: [with(StringComparer.Ordinal)]);

        using JsonDocument document = JsonDocument.Parse(Text(result));
        Assert.Equal(expected: "src/Example.slnx", actual: document.RootElement.GetProperty("solution").GetString());
        Assert.Equal(expected: 2, actual: document.RootElement.GetProperty("unitTestProjects").GetArrayLength());
        Assert.Equal(
            expected: "Example.Integration.Tests",
            actual: document.RootElement.GetProperty("integrationTestProjects")[0].GetString()
        );
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments)
    {
        IReadOnlyList<McpServerTool> tools = TestContexts.Tools(
            runner: this._runner,
            detection: TestRepositories.Detected(
                TestRepositories.Create(
                    root: this._repository.Path,
                    TestRepositories.CoreTests,
                    TestRepositories.ApiTests
                )
            )
        );

        return TestContexts
            .Find(tools: tools, name: name)
            .CallAsync(arguments: arguments, cancellationToken: this.CancellationToken());
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
