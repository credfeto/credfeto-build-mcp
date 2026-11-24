using System;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Detection;

public sealed class PythonOverridesTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task AProjectsDirectoryIsDetectedAndWatchedAsync()
    {
        await this.WriteAsync(relative: "tools/scripts/requirements.txt", content: "pytest\n");
        await this.WriteOverridesAsync("""{ "python": { "projects": ["tools/scripts"] } }""");

        PythonDetection detection = await this.DetectAsync();

        Assert.Null(detection.OverridesError);
        PythonProject project = Assert.Single(detection.Repository?.Projects ?? []);
        Assert.Equal(expected: "tools/scripts", actual: project.RelativeDirectory);
        Assert.Equal(
            expected: ["", "src", ".ai", "tools/scripts"],
            actual: detection.WatchedDirectories,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task ProjectsOutsideTheRepositoryAreRefusedWithoutRepeatingThemAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync("""{ "python": { "projects": ["../secret", "/etc", "a\\b", "missing"] } }""");

        PythonDetection detection = await this.DetectAsync();

        Assert.Equal(
            expected: "python.projects must list relative directories inside the repository, so 3 of them were ignored; python.projects names directories with no project: missing",
            actual: detection.OverridesError
        );
        Assert.Equal(expected: "example", actual: Assert.Single(detection.Repository?.Projects ?? []).Name);
    }

    [Fact]
    public async Task DisabledFindsNothingAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync("""{ "python": { "enabled": false } }""");

        PythonDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "Disabled by .ai/commands.json", actual: detection.Reason);
    }

    [Theory]
    [InlineData(1000, 240)]
    [InlineData(0, 1)]
    [InlineData(12, 12)]
    public async Task TheTimeoutIsClampedAsync(int configured, int expected)
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync(
            """{ "python": { "timeoutMinutes": """
                + configured.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " } }"
        );

        PythonDetection detection = await this.DetectAsync();

        Assert.Equal(expected: TimeSpan.FromMinutes(expected), actual: detection.Repository?.Timeout);
    }

    [Fact]
    public async Task AnUnknownPythonSettingVoidsOnlyThePythonSectionAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync(
            """{ "python": { "testCommand": "rm -rf /" }, "dotnet": { "timeoutMinutes": 5 } }"""
        );

        PythonDetection detection = await this.DetectAsync();

        Assert.NotNull(detection.Repository);
        Assert.Equal(
            expected: ".ai/commands.json: the python section is not valid (line 1) and was ignored",
            actual: detection.OverridesError
        );
    }

    [Fact]
    public async Task ProjectNamesAreUniqueAndNeverAllAsync()
    {
        await this.WriteAsync(relative: "pyproject.toml", content: "[project]\nname = \"all\"\n");
        await this.WriteAsync(relative: "src/pyproject.toml", content: "[project]\nname = \"tools\"\n");
        await this.WriteAsync(relative: "tools/pyproject.toml", content: "[project]\nname = \"tools\"\n");
        await this.WriteOverridesAsync("""{ "python": { "projects": ["tools"] } }""");

        PythonDetection detection = await this.DetectAsync();

        Assert.Equal(
            expected: [".", "tools", "tools-2"],
            actual: (detection.Repository?.Projects ?? []).Select(static project => project.Name),
            comparer: StringComparer.Ordinal
        );
    }

    private ValueTask WriteOverridesAsync(string content)
    {
        return this.WriteAsync(relative: ".ai/commands.json", content: content);
    }

    private ValueTask WriteAsync(string relative, string content)
    {
        return PythonRepositories.WriteAsync(
            repository: this._repository,
            relative: relative,
            content: content,
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask<PythonDetection> DetectAsync()
    {
        return PythonDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );
    }
}
