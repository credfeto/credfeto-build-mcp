using System;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Node.Server.Workspace;
using Credfeto.Build.Server.Workspace;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Workspace;

public sealed class NodeWorkspaceTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task TheWatchFilterListsTheDetectedDirectoriesWithoutRecursingAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "root", "workspaces": ["packages/*"] }""",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await NodeDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );
        WatchFilter filter = NodeWorkspace.WatchFilterFor(detection);

        Assert.False(condition: filter.Recursive, userMessage: "node_modules must never be watched recursively");
        Assert.Equal(
            expected: ["", "src", ".ai", "packages"],
            actual: filter.Directories,
            comparer: StringComparer.Ordinal
        );
        Assert.Contains(
            expected: "node_modules",
            collection: filter.IgnoredDirectories,
            comparer: StringComparer.Ordinal
        );
        Assert.Contains(expected: "package.json", collection: filter.FileNames, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "package-lock.json", collection: filter.FileNames, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "yarn.lock", collection: filter.FileNames, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "tsconfig.json", collection: filter.FileNames, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task TheSignatureChangesWhenATsConfigIsAddedAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        string before = NodeWorkspace.Signature(
            await NodeDetector.DetectAsync(
                repositoryRoot: this._repository.Path,
                cancellationToken: this.CancellationToken()
            )
        );

        _ = await this._repository.WriteAsync(
            relative: "tsconfig.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        string after = NodeWorkspace.Signature(
            await NodeDetector.DetectAsync(
                repositoryRoot: this._repository.Path,
                cancellationToken: this.CancellationToken()
            )
        );

        Assert.NotEqual(expected: before, actual: after, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task ChangesInNodeModulesAreIgnoredByTheWatcherAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        NodeDetection detection = await NodeDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );
        using NodeWorkspace workspace = new(
            initial: detection,
            runner: new RecordingJobRunner(),
            npm: TestTools.FakeNpm,
            resources: TestTools.FourGigabyteHost
        );
        await using RepositoryWatcher watcher = new(
            repositoryRoot: this._repository.Path,
            source: workspace,
            filter: NodeWorkspace.WatchFilterFor(detection),
            timeProvider: TimeProvider.System,
            debounce: TimeSpan.FromMilliseconds(50)
        );

        Assert.False(
            condition: watcher.IsRelevant(
                fullPath: this._repository.Combine("node_modules/left-pad/package.json"),
                mayBeDirectory: false
            ),
            userMessage: "a package inside node_modules is not a workspace"
        );
        Assert.False(
            condition: watcher.IsRelevant(fullPath: this._repository.Combine("node_modules"), mayBeDirectory: true),
            userMessage: "node_modules itself is ignored"
        );
        Assert.True(
            condition: watcher.IsRelevant(
                fullPath: this._repository.Combine("package-lock.json"),
                mayBeDirectory: false
            ),
            userMessage: "a lock file change counts"
        );
        Assert.True(
            condition: watcher.IsRelevant(
                fullPath: this._repository.Combine("src/package.json"),
                mayBeDirectory: false
            ),
            userMessage: "a manifest change counts"
        );
    }

    [Fact]
    public async Task TheToolsAreInNameOrderAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        NodeDetection detection = await NodeDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );
        using NodeWorkspace workspace = new(
            initial: detection,
            runner: new RecordingJobRunner(),
            npm: TestTools.FakeNpm,
            resources: TestTools.FourGigabyteHost
        );

        string[] names = [.. workspace.Tools.Select(static tool => tool.ProtocolTool.Name)];

        Assert.True(condition: names.Length > 1, userMessage: "an npm repository offers several tools");
        Assert.Equal(
            expected: names.Order(StringComparer.OrdinalIgnoreCase),
            actual: names,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task TheSignatureChangesWhenAScriptIsAddedAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        string before = NodeWorkspace.Signature(
            await NodeDetector.DetectAsync(
                repositoryRoot: this._repository.Path,
                cancellationToken: this.CancellationToken()
            )
        );

        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "example", "scripts": { "build": "tsc", "test": "vitest run", "lint": "eslint ." } }""",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );
        string after = NodeWorkspace.Signature(
            await NodeDetector.DetectAsync(
                repositoryRoot: this._repository.Path,
                cancellationToken: this.CancellationToken()
            )
        );

        Assert.NotEqual(expected: before, actual: after, comparer: StringComparer.Ordinal);
    }
}
