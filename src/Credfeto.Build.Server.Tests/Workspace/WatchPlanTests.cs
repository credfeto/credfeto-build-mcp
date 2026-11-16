using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Workspace;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Server.Tests.Workspace;

public sealed class WatchPlanTests : TestBase, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);

    // How long a change that should not be seen is given to show up anyway
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(600);

    private static readonly WatchFilter Plan = new(Extensions: [], IgnoredDirectories: ["node_modules", ".git"])
    {
        FileNames = ["package.json", "package-lock.json"],
        Recursive = false,
        Directories = ["", "src", ".ai"],
    };

    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task AListedDirectoryIsWatchedButNotTheDirectoriesUnderItAsync()
    {
        _ = Directory.CreateDirectory(this._repository.Combine("src/deep"));
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the start-up check", this.CancellationToken());

        _ = await this._repository.WriteAsync(
            relative: "src/deep/package.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        await Task.Delay(delay: Quiet, cancellationToken: this.CancellationToken());
        Assert.Equal(expected: 1, actual: handler.Calls);

        _ = await this._repository.WriteAsync(
            relative: "src/package.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        await Waiting.ForAsync(
            () => handler.Calls >= 2,
            what: "the listed directory's change",
            this.CancellationToken()
        );
    }

    [Fact]
    public async Task AListedDirectoryCreatedAfterStartUpIsWatchedAsync()
    {
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the start-up check", this.CancellationToken());

        _ = Directory.CreateDirectory(this._repository.Combine(".ai"));
        await Waiting.ForAsync(() => handler.Calls >= 2, what: "the new directory", this.CancellationToken());
        int afterDirectory = handler.Calls;

        _ = await this._repository.WriteAsync(
            relative: ".ai/commands.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        await Waiting.ForAsync(
            () => handler.Calls > afterDirectory,
            what: "the change in the new directory",
            this.CancellationToken()
        );
    }

    [Fact]
    public async Task AListedDirectoryThatIsRemovedAndCreatedAgainIsWatchedAgainAsync()
    {
        _ = Directory.CreateDirectory(this._repository.Combine("src"));
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the start-up check", this.CancellationToken());

        Directory.Delete(this._repository.Combine("src"));
        await Waiting.ForAsync(() => handler.Calls >= 2, what: "the removal", this.CancellationToken());
        _ = Directory.CreateDirectory(this._repository.Combine("src"));
        await Waiting.ForAsync(() => handler.Calls >= 3, what: "the new directory", this.CancellationToken());
        int afterDirectory = handler.Calls;

        _ = await this._repository.WriteAsync(
            relative: "src/package.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        await Waiting.ForAsync(
            () => handler.Calls > afterDirectory,
            what: "the change in the directory created again",
            this.CancellationToken()
        );
    }

    [Fact]
    public async Task AnUnlistedDirectoryIsNeverWatchedAsync()
    {
        _ = Directory.CreateDirectory(this._repository.Combine("node_modules/left-pad"));
        RecordingChangeHandler handler = new();
        await using RepositoryWatcher watcher = this.Create(handler);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");
        await Waiting.ForAsync(() => handler.Calls == 1, what: "the start-up check", this.CancellationToken());

        _ = await this._repository.WriteAsync(
            relative: "node_modules/left-pad/package.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        await Task.Delay(delay: Quiet, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: 1, actual: handler.Calls);
    }

    [Theory]
    [InlineData("package.json", true)]
    [InlineData("src/package-lock.json", true)]
    [InlineData("Package.json", false)]
    [InlineData("src/index.ts", false)]
    [InlineData("node_modules/x/package.json", false)]
    public async Task TheFileNamesCountExactlyAsync(string relative, bool relevant)
    {
        await using RepositoryWatcher watcher = this.Create(new RecordingChangeHandler());

        Assert.Equal(
            expected: relevant,
            actual: watcher.IsRelevant(fullPath: this._repository.Combine(relative), mayBeDirectory: false)
        );
    }

    private RepositoryWatcher Create(RecordingChangeHandler handler)
    {
        return new(
            repositoryRoot: this._repository.Path,
            source: handler,
            filter: Plan,
            timeProvider: TimeProvider.System,
            debounce: Debounce
        );
    }
}
