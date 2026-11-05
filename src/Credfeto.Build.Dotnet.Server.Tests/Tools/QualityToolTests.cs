using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Tools;

public sealed class QualityToolTests : TestBase, IDisposable
{
    private const string MANIFEST = """{ "version": 1, "isRoot": true, "tools": {} }""";

    private readonly TemporaryDirectory _repository = new();
    private readonly RecordingJobRunner _runner = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Theory]
    [InlineData("pre_release", "true")]
    [InlineData("release", "false")]
    public async Task BuildcheckRestoresToolsThenChecksTheSolutionAsync(string mode, string preRelease)
    {
        await this.WriteManifestAsync(".config/dotnet-tools.json");

        CallToolResult result = await this.CallAsync(name: "buildcheck", arguments: Arguments(name: "mode", value: mode));

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: 2, actual: definition.Steps.Count);
        this.AssertToolRestore(definition.Steps[0]);
        Assert.Equal(
            expected: ["buildcheck", "-Solution", this._repository.Combine("src/Example.slnx"), "-WarningAsErrors", "true", "-PreReleaseBuild", preRelease],
            actual: definition.Steps[1].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: this._repository.Path, actual: definition.Steps[1].WorkingDirectory);
        Assert.False(condition: definition.ContinueOnFailure, userMessage: "a failed tool restore stops the job");
    }

    [Theory]
    [InlineData("buildcheck")]
    [InlineData("cleanup")]
    [InlineData("changelog_lint")]
    public async Task QualityToolsWithoutAToolManifestFailClearlyAsync(string tool)
    {
        _ = await this._repository.WriteAsync(relative: "CHANGELOG.md", content: "# Changelog\n", cancellationToken: this.CancellationToken());

        CallToolResult result = await this.CallAsync(name: tool, arguments: [with(StringComparer.Ordinal)]);

        Assert.True(condition: result.IsError, userMessage: tool + " ran without a manifest");
        Assert.Contains(expectedSubstring: "no .NET tool manifest", actualString: Text(result), comparisonType: StringComparison.Ordinal);
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task ChangelogLintLintsTheRootChangelogAsync()
    {
        await this.WriteManifestAsync("dotnet-tools.json");
        string changelog = await this._repository.WriteAsync(relative: "CHANGELOG.md", content: "# Changelog\n", cancellationToken: this.CancellationToken());

        CallToolResult result = await this.CallAsync(name: "changelog_lint", arguments: [with(StringComparer.Ordinal)]);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        this.AssertToolRestore(definition.Steps[0]);
        Assert.Equal(expected: ["changelog", "--changelog", changelog, "--lint"], actual: definition.Steps[1].Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: this._repository.Path, actual: definition.Steps[1].WorkingDirectory);
    }

    [Fact]
    public async Task ChangelogLintWithoutAChangelogFailsClearlyAsync()
    {
        await this.WriteManifestAsync(".config/dotnet-tools.json");

        CallToolResult result = await this.CallAsync(name: "changelog_lint", arguments: [with(StringComparer.Ordinal)]);

        Assert.True(condition: result.IsError, userMessage: "linted a missing changelog");
        Assert.Contains(expectedSubstring: "CHANGELOG.md", actualString: Text(result), comparisonType: StringComparison.Ordinal);
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task BuildtestRunsTheInstalledScriptWithNoArgumentsFromTheRootAsync()
    {
        string script = await this._repository.WriteAsync(relative: "tools/buildtest", content: "#!/bin/sh\nexit 0\n", cancellationToken: this.CancellationToken());
        Executable.Mark(script);
        BuildtestLocator locator = new(name => StringComparer.Ordinal.Equals(x: name, y: BuildtestLocator.VARIABLE) ? script : null);

        CallToolResult result = await this.CallAsync(name: "buildtest", arguments: [with(StringComparer.Ordinal)], buildtest: locator);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep step = Assert.Single(Assert.Single(this._runner.Started).Steps);
        Assert.Equal(expected: script, actual: step.FileName);
        Assert.Empty(step.Arguments);
        Assert.Equal(expected: this._repository.Path, actual: step.WorkingDirectory);
    }

    [Fact]
    public async Task BuildtestWithoutTheScriptFailsClearlyAsync()
    {
        CallToolResult result = await this.CallAsync(name: "buildtest", arguments: [with(StringComparer.Ordinal)]);

        Assert.True(condition: result.IsError, userMessage: "ran a missing script");
        Assert.Contains(expectedSubstring: BuildtestLocator.VARIABLE, actualString: Text(result), comparisonType: StringComparison.Ordinal);
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task CleanupFormatsTheChangedFilesAndReportsWhatChangedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        await this.WriteManifestAsync(".config/dotnet-tools.json");
        await TestGit.RunAsync(repository: this._repository.Path, arguments: ["init", "--quiet"], cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/Old.cs", content: "class Old {}\n", cancellationToken: cancellationToken);
        await TestGit.CommitAllAsync(repository: this._repository.Path, message: "base", cancellationToken: cancellationToken);
        string changed = await this._repository.WriteAsync(relative: "src/New.cs", content: "class New{}\n", cancellationToken: cancellationToken);

        CallToolResult result = await this.CallAsync(name: "cleanup", arguments: [with(StringComparer.Ordinal)]);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        this.AssertToolRestore(definition.Steps[0]);
        Assert.Equal(expected: ["cscleanup", changed], actual: definition.Steps[1].Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: this._repository.Path, actual: definition.Steps[1].WorkingDirectory);

        // What the formatter would do; the probe reports it once the job's steps have run
        await File.WriteAllTextAsync(path: changed, contents: "class New { }\n", cancellationToken: cancellationToken);
        IChangedFilesProbe probe = definition.ChangedFiles ?? throw new InvalidOperationException("No changed-files probe");
        Assert.Equal(expected: ["src/New.cs"], actual: await probe.CollectAsync(cancellationToken), comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task CleanupWithNothingChangedStartsNothingAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        await this.WriteManifestAsync(".config/dotnet-tools.json");
        await TestGit.RunAsync(repository: this._repository.Path, arguments: ["init", "--quiet"], cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/Old.cs", content: "class Old {}\n", cancellationToken: cancellationToken);
        await TestGit.CommitAllAsync(repository: this._repository.Path, message: "base", cancellationToken: cancellationToken);

        CallToolResult result = await this.CallAsync(name: "cleanup", arguments: [with(StringComparer.Ordinal)]);

        Assert.True(condition: result.IsError, userMessage: Text(result));
        Assert.Contains(expectedSubstring: "nothing to clean up", actualString: Text(result), comparisonType: StringComparison.Ordinal);
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public void CleanupOfManyFilesIsSplitIntoBatches()
    {
        DotnetRepository repository = TestRepositories.Create(root: this._repository.Path);
        string[] files = [.. Enumerable.Range(start: 0, count: CleanupTool.FILES_PER_STEP * 2 + 1).Select(index => "/repo/F" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".cs")];

        IReadOnlyList<ProcessStep> steps = CleanupTool.Steps(repository: repository, files: files);

        Assert.Equal(expected: ["cscleanup 1/3", "cscleanup 2/3", "cscleanup 3/3"], actual: steps.Select(static step => step.Name), comparer: StringComparer.Ordinal);
        Assert.Equal(expected: files, actual: steps.SelectMany(static step => step.Arguments.Skip(1)), comparer: StringComparer.Ordinal);
    }

    private void AssertToolRestore(ProcessStep step)
    {
        Assert.Equal(expected: "dotnet", actual: step.FileName);
        Assert.Equal(expected: ["tool", "restore"], actual: step.Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: this._repository.Path, actual: step.WorkingDirectory);
    }

    private async ValueTask WriteManifestAsync(string relative)
    {
        _ = await this._repository.WriteAsync(relative: relative, content: MANIFEST, cancellationToken: this.CancellationToken());
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments)
    {
        return this.CallAsync(name: name, arguments: arguments, buildtest: TestContexts.NoBuildtest);
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments, BuildtestLocator buildtest)
    {
        DotnetDetection detection = TestRepositories.Detected(TestRepositories.Create(root: this._repository.Path, TestRepositories.CoreTests));
        ToolContext context = TestContexts.Create(runner: this._runner, detection: detection, buildtest: buildtest);

        return TestContexts
            .Find(tools: DotnetToolCatalog.Create(detection: detection, context: context), name: name)
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
