using System;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Python.Server.Workspace;
using Credfeto.Build.Server.Workspace;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Workspace;

public sealed class PythonWorkspaceTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task TheWatchFilterListsTheDirectoriesWithoutRecursingAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());

        WatchFilter filter = PythonWorkspace.WatchFilterFor(await this.DetectAsync());

        Assert.False(
            condition: filter.Recursive,
            userMessage: "a virtual environment must never be watched recursively"
        );
        Assert.Equal(expected: ["", "src", ".ai"], actual: filter.Directories, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: ".venv", collection: filter.IgnoredDirectories, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "pyproject.toml", collection: filter.FileNames, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "uv.lock", collection: filter.FileNames, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "poetry.lock", collection: filter.FileNames, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: ".txt", collection: filter.Extensions, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task ChangesInsideTheVirtualEnvironmentAreIgnoredByTheWatcherAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        PythonDetection detection = await this.DetectAsync();
        using PythonWorkspace workspace = new(
            initial: detection,
            runner: new RecordingJobRunner(),
            python: TestTools.FakePython
        );
        await using RepositoryWatcher watcher = new(
            repositoryRoot: this._repository.Path,
            source: workspace,
            filter: PythonWorkspace.WatchFilterFor(detection),
            timeProvider: TimeProvider.System,
            debounce: TimeSpan.FromMilliseconds(50)
        );

        Assert.False(
            condition: watcher.IsRelevant(
                fullPath: this._repository.Combine(".venv/lib/python3.13/site-packages/pkg/pyproject.toml"),
                mayBeDirectory: false
            ),
            userMessage: "a manifest inside the virtual environment is not the project's"
        );
        Assert.False(
            condition: watcher.IsRelevant(fullPath: this._repository.Combine(".venv"), mayBeDirectory: true),
            userMessage: ".venv itself is ignored"
        );
        Assert.True(
            condition: watcher.IsRelevant(
                fullPath: this._repository.Combine("requirements-dev.txt"),
                mayBeDirectory: false
            ),
            userMessage: "a requirements file counts"
        );
        Assert.True(
            condition: watcher.IsRelevant(fullPath: this._repository.Combine("src/uv.lock"), mayBeDirectory: false),
            userMessage: "a lock file counts"
        );
    }

    [Fact]
    public async Task TheSignatureChangesWhenAUvLockIsAddedAsync()
    {
        await PythonRepositories.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        string before = PythonWorkspace.Signature(await this.DetectAsync());

        _ = await this._repository.WriteAsync(
            relative: "uv.lock",
            content: "version = 1\n",
            cancellationToken: this.CancellationToken()
        );
        string after = PythonWorkspace.Signature(await this.DetectAsync());

        Assert.NotEqual(expected: before, actual: after, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task TheSignatureChangesWhenATestSuiteAppearsAsync()
    {
        string before = PythonWorkspace.Signature(await this.DetectAsync());

        _ = await this._repository.WriteAsync(
            relative: "tests/test_example.py",
            content: "import unittest\n",
            cancellationToken: this.CancellationToken()
        );
        string after = PythonWorkspace.Signature(await this.DetectAsync());

        Assert.NotEqual(expected: before, actual: after, comparer: StringComparer.Ordinal);
    }

    private ValueTask<PythonDetection> DetectAsync()
    {
        return PythonDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );
    }
}
