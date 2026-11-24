using System;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Detection;

public sealed class PythonDetectorTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task ARootPyprojectWithAProjectTableIsDetectedAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());

        PythonRepository repository = await this.DetectRepositoryAsync();

        PythonProject project = Assert.Single(repository.Projects);
        Assert.Equal(expected: "example", actual: project.Name);
        Assert.Equal(expected: ".", actual: project.RelativeDirectory);
        Assert.Equal(expected: PythonInstaller.Pip, actual: project.Installer);
        Assert.Equal(expected: ["pyproject.toml"], actual: project.Manifests, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: ">=3.11", actual: project.RequiresPython);
        Assert.Equal(
            expected: ["httpx>=0.27", "feedparser"],
            actual: project.Dependencies,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ["dev"], actual: project.TestExtras, comparer: StringComparer.Ordinal);
        Assert.Equal(
            expected: ["pytest", "coverage[toml]"],
            actual: project.TestRequirements,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: project.DeclaresPytest, userMessage: "pytest is in the dev extra");
        Assert.True(condition: project.DeclaresCoverage, userMessage: "coverage is in the dev extra");
        Assert.Equal(expected: TimeSpan.FromMinutes(30), actual: repository.Timeout);
    }

    [Fact]
    public async Task APyprojectWithOnlyToolSettingsIsNotAProjectAsync()
    {
        await this.WriteAsync(relative: "pyproject.toml", content: "[tool.black]\nline-length = 120\n");

        PythonDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Null(detection.TestSuite);
        Assert.Equal(
            expected: "No pyproject.toml with a [project] table or requirements*.txt at the repository root or in src/",
            actual: detection.Reason
        );
    }

    [Fact]
    public async Task RequirementsFilesAloneMakeAProjectAsync()
    {
        await this.WriteAsync(relative: "requirements.txt", content: "httpx==0.27.0\n");
        await this.WriteAsync(
            relative: "requirements-dev.txt",
            content: "# test tools\n-r requirements.txt\nPyTest>=8 # the runner\npytest-cov\n"
        );

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: ".", actual: project.Name);
        Assert.Equal(expected: PythonInstaller.Pip, actual: project.Installer);
        Assert.Equal(
            expected: ["requirements-dev.txt", "requirements.txt"],
            actual: project.RequirementFiles,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: project.DeclaresPytest, userMessage: "PyTest is pytest");
        Assert.True(condition: project.DeclaresCoverage, userMessage: "pytest-cov brings coverage");
    }

    [Fact]
    public async Task AProjectInSrcIsDetectedAsync()
    {
        await this.WriteAsync(relative: "src/requirements.txt", content: "pytest\n");

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: "src", actual: project.RelativeDirectory);
        Assert.Equal(expected: "src", actual: project.Name);
        Assert.False(condition: project.DeclaresCoverage, userMessage: "coverage is not declared");
    }

    [Fact]
    public async Task AUvLockMeansUvAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteAsync(relative: "uv.lock", content: "version = 1\n");

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: PythonInstaller.Uv, actual: project.Installer);
        Assert.Equal(
            expected: ["pyproject.toml", "uv.lock"],
            actual: project.Manifests,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task AUvLockWithoutAProjectTableIsUnsupportedAsync()
    {
        await this.WriteAsync(relative: "requirements.txt", content: "pytest\n");
        await this.WriteAsync(relative: "uv.lock", content: "version = 1\n");

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: PythonInstaller.Unsupported, actual: project.Installer);
        Assert.Equal(expected: "uv.lock needs a pyproject.toml with a [project] table", actual: project.Problem);
    }

    [Theory]
    [InlineData("poetry.lock", "poetry")]
    [InlineData("Pipfile", "pipenv")]
    [InlineData("Pipfile.lock", "pipenv")]
    [InlineData("pdm.lock", "pdm")]
    public async Task AnotherManagersFilesAreReportedAsUnsupportedAsync(string fileName, string manager)
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteAsync(relative: fileName, content: "# lock\n");

        PythonRepository repository = await this.DetectRepositoryAsync();

        PythonProject project = Assert.Single(repository.Projects);
        Assert.Equal(expected: PythonInstaller.Unsupported, actual: project.Installer);
        Assert.Equal(
            expected: fileName + " means " + manager + ", which is not supported: only pip and uv are",
            actual: project.Problem
        );
        Assert.Empty(repository.SupportedProjects);
    }

    [Fact]
    public async Task APoetryOnlyPyprojectIsUnsupportedAsync()
    {
        await this.WriteAsync(relative: "pyproject.toml", content: "[tool.poetry]\nname = \"legacy\"\n");

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: PythonInstaller.Unsupported, actual: project.Installer);
        Assert.Equal(
            expected: "pyproject.toml describes a poetry project, which is not supported: only pip and uv are",
            actual: project.Problem
        );
    }

    [Fact]
    public async Task APyprojectThatIsNotValidTomlIsReportedWithoutItsContentAsync()
    {
        await this.WriteAsync(relative: "src/pyproject.toml", content: "[project\nname = \"secret-value\"\n");

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: PythonInstaller.Unsupported, actual: project.Installer);
        Assert.Equal(expected: "src/pyproject.toml is not valid TOML", actual: project.Problem);
    }

    [Fact]
    public async Task APyprojectOverOneMebibyteIsReportedAsync()
    {
        await this.WriteAsync(
            relative: "pyproject.toml",
            content: "[project]\nname = \"big\"\n#" + new string(c: 'x', count: 1024 * 1024) + "\n"
        );

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Equal(expected: "pyproject.toml is larger than 1 MiB", actual: project.Problem);
    }

    [Fact]
    public async Task DependencyGroupsAreReadWithTheirIncludedGroupsAsync()
    {
        await this.WriteAsync(
            relative: "pyproject.toml",
            content: """
            [project]
            name = "grouped"
            version = "1.0"

            [dependency-groups]
            test = ["pytest>=8", { include-group = "cov" }]
            cov = ["coverage"]
            lint = ["ruff"]
            """
        );

        PythonProject project = Assert.Single((await this.DetectRepositoryAsync()).Projects);

        Assert.Empty(project.TestExtras);
        Assert.Equal(expected: ["test"], actual: project.TestGroups, comparer: StringComparer.Ordinal);
        Assert.Equal(
            expected: ["pytest>=8", "coverage"],
            actual: project.TestRequirements,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: project.DeclaresCoverage, userMessage: "coverage comes from the included group");
    }

    [Fact]
    public async Task NoManifestAndNoTestsIsNotDetectedAsync()
    {
        await this.WriteAsync(relative: "README.md", content: "# Example\n");

        PythonDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Null(detection.TestSuite);
    }

    [Fact]
    public async Task TestFilesWithNoManifestAreReportedWithTheirDirectoriesAsync()
    {
        await this.WriteAsync(relative: "scripts/tests/test_zone.py", content: "import unittest\n");
        await this.WriteAsync(relative: ".github/actions/check/test_check.py", content: "import unittest\n");
        await this.WriteAsync(relative: "lib/parser_test.py", content: "import unittest\n");
        await this.WriteAsync(relative: "lib/parser.py", content: "x = 1\n");
        await this.WriteAsync(relative: ".venv/lib/test_inside_venv.py", content: "x = 1\n");
        await this.WriteAsync(relative: "node_modules/pkg/test_inside.py", content: "x = 1\n");

        PythonDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.NotNull(detection.TestSuite);
        Assert.Equal(
            expected: [".github/actions/check", "lib", "scripts/tests"],
            actual: detection.TestSuite.TestDirectories,
            comparer: StringComparer.Ordinal
        );
        Assert.EndsWith(
            expectedEndString: "but there are Python test files that nothing declares",
            actualString: detection.Reason,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task TestFilesOnlyInsideAVirtualEnvironmentDoNotCountAsync()
    {
        await this.WriteAsync(relative: ".venv/lib/test_inside_venv.py", content: "x = 1\n");
        await this.WriteAsync(relative: "venv/lib/test_inside_venv.py", content: "x = 1\n");

        PythonDetection detection = await this.DetectAsync();

        Assert.Null(detection.TestSuite);
    }

    [Fact]
    public async Task AMissingRepositoryRootIsNotDetectedAsync()
    {
        PythonDetection detection = await PythonDetector.DetectAsync(
            repositoryRoot: this._repository.Combine("missing"),
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "The repository root does not exist", actual: detection.Reason);
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

    private async ValueTask<PythonRepository> DetectRepositoryAsync()
    {
        PythonDetection detection = await this.DetectAsync();

        Assert.NotNull(detection.Repository);

        return detection.Repository;
    }
}
