using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tools;
using Credfeto.Build.Dotnet.Server.Workspace;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Workspace;

public sealed class DotnetWorkspaceTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();
    private readonly RecordingJobRunner _runner = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task ANewProjectRebuildsTheEnumsInOneChangeAsync()
    {
        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        using DotnetWorkspace workspace = await this.CreateAsync();
        int changes = 0;
        workspace.Tools.Changed += (_, _) => Interlocked.Increment(ref changes);

        await SampleRepository.AddProjectAsync(
            repository: this._repository,
            name: "Example.New.Tests",
            cancellationToken: this.CancellationToken()
        );
        _ = await workspace.RefreshAsync(this.CancellationToken());

        Assert.Equal(expected: 1, actual: changes);
        Assert.Contains(
            expected: "Example.New.Tests",
            collection: ProjectEnum(workspace: workspace, tool: "test"),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task AnUnchangedRepositoryChangesNothingAsync()
    {
        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        using DotnetWorkspace workspace = await this.CreateAsync();
        int changes = 0;
        workspace.Tools.Changed += (_, _) => Interlocked.Increment(ref changes);

        _ = await workspace.RefreshAsync(this.CancellationToken());

        Assert.Equal(expected: 0, actual: changes);
    }

    [Fact]
    public async Task ToolsAppearWithDotnetAndGoWithItAsync()
    {
        using DotnetWorkspace workspace = await this.CreateAsync();
        Assert.True(condition: workspace.Tools.IsEmpty, userMessage: "no .NET, no tools");

        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        _ = await workspace.RefreshAsync(this.CancellationToken());
        Assert.Contains(
            expected: "buildtest",
            collection: workspace.Tools.PrimitiveNames,
            comparer: StringComparer.Ordinal
        );

        File.Delete(this._repository.Combine("src/Example.slnx"));
        _ = await workspace.RefreshAsync(this.CancellationToken());
        Assert.True(condition: workspace.Tools.IsEmpty, userMessage: "the solution went, so did the tools");
    }

    [Fact]
    public async Task AProjectAddedAfterTheSchemaWasPublishedIsAcceptedAsync()
    {
        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        using DotnetWorkspace workspace = await this.CreateAsync();
        BuildTool test = TestContexts.Find(tools: workspace.Tools, name: "test");

        // The client never re-reads the schema: it calls the tool it already has with the new project
        await SampleRepository.AddProjectAsync(
            repository: this._repository,
            name: "Example.New.Tests",
            cancellationToken: this.CancellationToken()
        );
        CallToolResult result = await test.CallAsync(
            arguments: ProjectArguments("Example.New.Tests"),
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(expected: "test Example.New.Tests", actual: Assert.Single(this._runner.Started).Steps[^1].Name);
    }

    [Fact]
    public async Task ADeletedProjectIsRefusedAsync()
    {
        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        using DotnetWorkspace workspace = await this.CreateAsync();
        BuildTool test = TestContexts.Find(tools: workspace.Tools, name: "test");
        BuildTool coverage = TestContexts.Find(tools: workspace.Tools, name: "coverage");

        Directory.Delete(path: this._repository.Combine("src/Example.Api.Tests"), recursive: true);
        CallToolResult tested = await test.CallAsync(
            arguments: ProjectArguments("Example.Api.Tests"),
            cancellationToken: this.CancellationToken()
        );
        CallToolResult measured = await coverage.CallAsync(
            arguments: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["project"] = String("Example.Api.Tests"),
            },
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: tested.IsError, userMessage: "test ran a deleted project");
        Assert.True(condition: measured.IsError, userMessage: "coverage ran a deleted project");
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task ACallAfterTheSolutionWentFailsClearlyAsync()
    {
        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        using DotnetWorkspace workspace = await this.CreateAsync();
        BuildTool build = TestContexts.Find(tools: workspace.Tools, name: "build");

        File.Delete(this._repository.Combine("src/Example.slnx"));
        CallToolResult result = await build.CallAsync(arguments: [], cancellationToken: this.CancellationToken());

        Assert.True(condition: result.IsError, userMessage: "built a missing solution");
        Assert.Contains(
            expectedSubstring: "no .NET solution",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._runner.Started);
    }

    private async ValueTask<DotnetWorkspace> CreateAsync()
    {
        DotnetDetection detection = await DotnetDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );

        return new(initial: detection, runner: this._runner, buildtest: TestContexts.NoBuildtest);
    }

    private static IReadOnlyList<string> ProjectEnum(DotnetWorkspace workspace, string tool)
    {
        McpServerTool found = TestContexts.Find(tools: workspace.Tools, name: tool);

        return
        [
            .. found
                .ProtocolTool.InputSchema.GetProperty("properties")
                .GetProperty("project")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(static value => value.GetString() ?? string.Empty),
        ];
    }

    private static Dictionary<string, JsonElement> ProjectArguments(string project)
    {
        return new(StringComparer.Ordinal) { ["scope"] = String("project"), ["project"] = String(project) };
    }

    private static JsonElement String(string value)
    {
        return JsonSerializer.SerializeToElement(value: value, jsonTypeInfo: TestJsonContext.Default.String);
    }

    private static string Text(CallToolResult result)
    {
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }
}
