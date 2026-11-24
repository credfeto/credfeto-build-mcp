using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Testing;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Python.Server.Tools;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Tools;

public sealed class TestAndLintToolTests : TestBase, IDisposable
{
    private readonly JobToolFixture _fixture = new();

    public void Dispose()
    {
        this._fixture.Dispose();
    }

    [Fact]
    public async Task TestInstallsFirstWhenTheVenvIsMissingAsync()
    {
        await this.ProjectAsync();

        CallToolResult result = await this.CallAsync(ToolNames.TEST, ("package", "example"));

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        Assert.Equal(
            expected: ["venv example", "install example (wheels only)", "stamp example", "test example"],
            actual: this._fixture.StepNames(),
            comparer: StringComparer.Ordinal
        );
        JobDefinition job = this._fixture.Runner.Started.Single();
        Assert.IsType<PytestOutputParser>(job.TestParser);
        Assert.True(condition: job.ContinueOnFailure, userMessage: "a failing test run still reports its counts");
    }

    [Fact]
    public async Task TestSkipsTheInstallWhenTheStampIsCurrentAsync()
    {
        await this.ProjectAsync();
        await this.StampAsync();

        CallToolResult result = await this.CallAsync(ToolNames.TEST, ("package", "all"));

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        Assert.Equal(expected: ["test example"], actual: this._fixture.StepNames(), comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task AChangedManifestMakesTheStampStaleAsync()
    {
        await this.ProjectAsync();
        await this.StampAsync();
        await this._fixture.WriteAsync(
            relative: "pyproject.toml",
            content: PythonRepositories.SAMPLE_PYPROJECT + "\n# changed\n",
            cancellationToken: this.CancellationToken()
        );

        _ = await this.CallAsync(ToolNames.TEST, ("package", "all"));

        Assert.Equal(
            expected: ["install example (wheels only)", "stamp example", "test example"],
            actual: this._fixture.StepNames(),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task LintRunsTheHookAndSuperLinterLintersOverTrackedFilesAsync()
    {
        await this.ProjectAsync();
        await this.StampAsync();
        await this._fixture.WriteAsync(
            relative: "example.py",
            content: "X = 1\n",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "tests/test_example.py",
            content: "X = 2\n",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: LintTool.SUPER_LINTER_PYLINT_RC,
            content: "[MASTER]\n",
            cancellationToken: this.CancellationToken()
        );
        await TestGit.CommitAllAsync(
            repository: this._fixture.Repository.Path,
            message: "initial",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.CallAsync(ToolNames.LINT, ("scope", "all"));

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        JobDefinition job = this._fixture.Runner.Started.Single();
        Assert.Equal(
            expected: ["flake8 example", "pylint example", "pylint example (.github/linters/.python-lint)"],
            actual: job.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "/usr/bin/flake8", actual: job.Steps[0].FileName);
        Assert.Equal(
            expected: ["example.py", "tests/test_example.py"],
            actual: job.Steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "/usr/bin/pylint", actual: job.Steps[1].FileName);
    }

    [Fact]
    public async Task LintChangedTakesOnlyChangedAndNewFilesAsync()
    {
        await this.ProjectAsync();
        await this.StampAsync();
        await this._fixture.WriteAsync(
            relative: "example.py",
            content: "X = 1\n",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "same.py",
            content: "X = 1\n",
            cancellationToken: this.CancellationToken()
        );
        await TestGit.CommitAllAsync(
            repository: this._fixture.Repository.Path,
            message: "initial",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "example.py",
            content: "X = 3\n",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "added.py",
            content: "X = 4\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.CallAsync(ToolNames.LINT, ("scope", "changed"));

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        JobDefinition job = this._fixture.Runner.Started.Single();
        Assert.Equal(
            expected: ["added.py", "example.py"],
            actual: job.Steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: 2, actual: job.Steps.Count);
    }

    [Fact]
    public async Task LintWithNothingChangedSaysSoAsync()
    {
        await this.ProjectAsync();
        await this.StampAsync();
        await TestGit.CommitAllAsync(
            repository: this._fixture.Repository.Path,
            message: "initial",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.CallAsync(ToolNames.LINT, ("scope", "changed"));

        Assert.True(condition: result.IsError, userMessage: "nothing to lint");
        Assert.Contains(
            expectedSubstring: "There are no Python files to lint",
            actualString: TestTools.Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    private async ValueTask ProjectAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await PythonRepositories.WriteAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
    }

    // A venv installed from the manifests as they are now
    private async ValueTask StampAsync()
    {
        PythonDetection detection = await PythonDetector.DetectAsync(
            repositoryRoot: this._fixture.Repository.Path,
            cancellationToken: this.CancellationToken()
        );
        PythonProject project = detection.Repository?.Projects.Single()
            ?? throw new InvalidOperationException("no project");
        VirtualEnvironment environment = VirtualEnvironment.For(
            repositoryRoot: this._fixture.Repository.Path,
            project: project
        );
        string stamp = await VirtualEnvironment.StampAsync(
            environment: environment,
            project: project,
            interpreter: "/usr/bin/python3",
            version: new(Major: 3, Minor: 13, Patch: 1),
            cancellationToken: this.CancellationToken()
        );
        _ = await this._fixture.Repository.WriteAsync(
            relative: ".venv/bin/python",
            content: string.Empty,
            cancellationToken: this.CancellationToken()
        );
        _ = await this._fixture.Repository.WriteAsync(
            relative: ".venv/" + VirtualEnvironment.STAMP_FILE,
            content: stamp + "\n",
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask<CallToolResult> CallAsync(string tool, (string Name, string Value) argument)
    {
        return this._fixture.CallAsync(
            tool: tool,
            arguments: TestTools.Arguments(argument),
            cancellationToken: this.CancellationToken()
        );
    }
}
