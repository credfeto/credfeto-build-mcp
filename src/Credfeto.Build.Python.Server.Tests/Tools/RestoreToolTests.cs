using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Python.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Tools;

public sealed class RestoreToolTests : TestBase, IDisposable
{
    private readonly JobToolFixture _fixture = new();

    public void Dispose()
    {
        this._fixture.Dispose();
    }

    [Fact]
    public async Task RestoreInstallsAndSaysWhichTestToolsAreUndeclaredAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "requirements.txt",
            content: "httpx\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.RestoreAsync();

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        Assert.Equal(
            expected: ["venv .", "install . (wheels only)", "stamp ."],
            actual: this._fixture.StepNames(),
            comparer: StringComparer.Ordinal
        );
        using JsonDocument document = JsonDocument.Parse(TestTools.Text(result));
        string note = document.RootElement.GetProperty("notes")[0].GetString() ?? string.Empty;
        Assert.StartsWith(
            expectedStartString: "Project . does not declare pytest or coverage, so it is installed unpinned",
            actualString: note,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "Declare pytest and coverage in a test extra, dependency group or requirements file",
            actualString: note,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task RestoreHasNoNotesWhenTheToolsAreDeclaredAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await PythonRepositories.WriteAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.RestoreAsync();

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        using JsonDocument document = JsonDocument.Parse(TestTools.Text(result));
        Assert.False(
            condition: document.RootElement.TryGetProperty(propertyName: "notes", value: out _),
            userMessage: "nothing to say"
        );
    }

    [Fact]
    public async Task AVenvGitWouldCommitIsRefusedAsync()
    {
        await TestGitInitOnlyAsync();
        await this._fixture.WriteAsync(
            relative: "requirements.txt",
            content: "pytest\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.RestoreAsync();

        Assert.True(condition: result.IsError, userMessage: "the venv would be committed");
        Assert.Contains(
            expectedSubstring: ".venv is not ignored by git",
            actualString: TestTools.Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._fixture.Runner.Started);

        async Task TestGitInitOnlyAsync()
        {
            await Credfeto.Build.Testing.TestGit.RunAsync(
                repository: this._fixture.Repository.Path,
                arguments: ["init", "--quiet"],
                cancellationToken: this.CancellationToken()
            );
        }
    }

    [Fact]
    public async Task AnInterpreterOutsideRequiresPythonIsRefusedAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "pyproject.toml",
            content: "[project]\nname = \"future\"\nrequires-python = \">=3.99\"\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.RestoreAsync();

        Assert.True(condition: result.IsError, userMessage: "3.13 is too old");
        Assert.Contains(
            expectedSubstring: "python3 3.13.1 does not satisfy the requires-python of future",
            actualString: TestTools.Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ARequirementThatIsAnOptionIsRefusedAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "pyproject.toml",
            content: "[project]\nname = \"sneaky\"\ndependencies = [\"--index-url=https://example.com/simple\"]\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.RestoreAsync();

        Assert.True(condition: result.IsError, userMessage: "an option is not a requirement");
        Assert.DoesNotContain(
            expectedSubstring: "example.com",
            actualString: TestTools.Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ATestSuiteWithoutAManifestGetsAMinimalPyprojectAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "scripts/tests/test_zone.py",
            content: "import unittest\n",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: ".github/actions/check/test_check.py",
            content: "import unittest\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this._fixture.CallAsync(
            tool: ToolNames.RESTORE,
            arguments: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        string created = await File.ReadAllTextAsync(
            path: this._fixture.Repository.Combine("pyproject.toml"),
            cancellationToken: this.CancellationToken()
        );
        (PyprojectManifest? manifest, string? problem) = PyprojectReader.Parse(
            text: created,
            displayPath: "pyproject.toml"
        );
        Assert.Null(problem);
        Assert.NotNull(manifest);
        Assert.True(condition: manifest.HasProjectTable, userMessage: "the created file is a project");
        Assert.Contains(
            expectedSubstring: "    \".github/actions/check\",\n    \"scripts/tests\",\n",
            actualString: created,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Equal(
            expected: ["pytest", "coverage"],
            actual: manifest.OptionalDependencies["test"],
            comparer: StringComparer.Ordinal
        );
        using JsonDocument document = JsonDocument.Parse(TestTools.Text(result));
        Assert.StartsWith(
            expectedStartString: "There was no manifest for the Python tests, so restore created a minimal pyproject.toml",
            actualString: document.RootElement.GetProperty("notes")[0].GetString(),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Single(this._fixture.Runner.Started);
    }

    [Fact]
    public async Task ATestSuiteNextToAToolOnlyPyprojectGetsARequirementsFileAsync()
    {
        await PythonRepositories.InitGitAsync(
            repository: this._fixture.Repository,
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "pyproject.toml",
            content: "[tool.black]\nline-length = 120\n",
            cancellationToken: this.CancellationToken()
        );
        await this._fixture.WriteAsync(
            relative: "tests/test_a.py",
            content: "import unittest\n",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this._fixture.CallAsync(
            tool: ToolNames.RESTORE,
            arguments: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        Assert.Equal(
            expected: "[tool.black]\nline-length = 120\n",
            actual: await File.ReadAllTextAsync(
                path: this._fixture.Repository.Combine("pyproject.toml"),
                cancellationToken: this.CancellationToken()
            )
        );
        Assert.Equal(
            expected: ["pytest", "coverage"],
            actual: RequirementsReader.Parse(
                await File.ReadAllTextAsync(
                    path: this._fixture.Repository.Combine("requirements-dev.txt"),
                    cancellationToken: this.CancellationToken()
                )
            ),
            comparer: StringComparer.Ordinal
        );
    }

    private ValueTask<CallToolResult> RestoreAsync()
    {
        return this._fixture.CallAsync(
            tool: ToolNames.RESTORE,
            arguments: TestTools.Arguments(("package", "all")),
            cancellationToken: this.CancellationToken()
        );
    }
}
