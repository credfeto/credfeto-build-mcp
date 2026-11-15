using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Workspace;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Server.Tests.Workspace;

public sealed class RepositoryWatcherTests : TestBase, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);

    private static readonly WatchFilter Filter = new(Extensions: [".proj"], IgnoredDirectories: ["out", ".git"]);

    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task ARelevantChangeReachesTheHandlerAsync()
    {
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the start-up check", this.CancellationToken());

        _ = await this._repository.WriteAsync(
            relative: "src/Example.proj",
            content: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        await Waiting.ForAsync(() => handler.Calls >= 2, what: "the change", this.CancellationToken());
    }

    [Fact]
    public async Task RenamesAndDeletesReachTheHandlerAsync()
    {
        string project = await this._repository.WriteAsync(
            relative: "Example.proj",
            content: "<Project />",
            cancellationToken: this.CancellationToken()
        );
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the start-up check", this.CancellationToken());

        string renamed = this._repository.Combine("Renamed.proj");
        File.Move(sourceFileName: project, destFileName: renamed);
        await Waiting.ForAsync(() => handler.Calls >= 2, what: "the rename", this.CancellationToken());
        int afterRename = handler.Calls;

        File.Delete(renamed);
        await Waiting.ForAsync(() => handler.Calls > afterRename, what: "the delete", this.CancellationToken());
    }

    [Fact]
    public async Task ATransientFailureLeavesTheWatchRunningAsync()
    {
        RecordingChangeHandler handler = new() { FailFirstCall = true };
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the failing start-up check", this.CancellationToken());

        _ = await this._repository.WriteAsync(
            relative: "Example.proj",
            content: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        await Waiting.ForAsync(
            () => handler.Calls >= 2,
            what: "the change after the failure",
            this.CancellationToken()
        );
        Assert.True(condition: watcher.IsWatching, userMessage: "still watching");
    }

    [Fact]
    public async Task AMissingRepositoryIsNotWatchedAsync()
    {
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = new(
            repositoryRoot: this._repository.Combine("missing"),
            source: handler,
            filter: Filter,
            timeProvider: TimeProvider.System,
            debounce: Debounce
        );

        Assert.False(condition: watcher.Start(), userMessage: "nothing to watch");
        Assert.False(condition: watcher.IsWatching, userMessage: "nothing is watched");
    }

    [Fact]
    public async Task ADisposedWatcherStopsWatchingAsync()
    {
        RecordingChangeHandler handler = new();
        RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");

        await watcher.DisposeAsync();

        Assert.False(condition: watcher.IsWatching, userMessage: "the watch stopped");
        Assert.False(condition: watcher.Start(), userMessage: "a disposed watcher does not start again");
    }

    [Theory]
    [InlineData("src/out/Example.proj", true)]
    [InlineData(".git/Example.proj", true)]
    [InlineData("src/Example.proj", false)]
    [InlineData("out.proj", false)]
    public async Task TheFiltersIgnoredDirectoriesAreIgnoredAsync(string relative, bool ignored)
    {
        await using RepositoryWatcher watcher = this.Create(new RecordingChangeHandler());

        Assert.Equal(expected: ignored, actual: watcher.IsIgnored(this._repository.Combine(relative)));
    }

    [Theory]
    [InlineData("src/Example.proj", false, true)]
    [InlineData("src/Example.PROJ", false, true)]
    [InlineData(".ai/commands.json", false, true)]
    [InlineData("src/Program.cs", false, false)]
    [InlineData("src/Example", true, true)]
    [InlineData("src/out", true, false)]
    [InlineData("src/out/Example.proj", false, false)]
    public async Task OnlyTheFiltersFilesAndDirectoriesCountAsync(string relative, bool mayBeDirectory, bool relevant)
    {
        await using RepositoryWatcher watcher = this.Create(new RecordingChangeHandler());

        Assert.Equal(
            expected: relevant,
            actual: watcher.IsRelevant(fullPath: this._repository.Combine(relative), mayBeDirectory: mayBeDirectory)
        );
    }

    private RepositoryWatcher Create(RecordingChangeHandler handler)
    {
        return new(
            repositoryRoot: this._repository.Path,
            source: handler,
            filter: Filter,
            timeProvider: TimeProvider.System,
            debounce: Debounce
        );
    }
}
