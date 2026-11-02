using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Workspace;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Workspace;

/// <summary>The real file system watcher, with a short debounce, against a temporary repository.</summary>
public sealed class RepositoryWatcherTests : TestBase, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task ToolsFollowTheRepositoryWithoutAnyCallAsync()
    {
        using DotnetWorkspace workspace = await this.CreateWorkspaceAsync();
        await using RepositoryWatcher watcher = new(repositoryRoot: this._repository.Path, source: workspace, timeProvider: TimeProvider.System, debounce: Debounce);
        Assert.True(condition: watcher.Start(), userMessage: "the watch started");

        await SampleRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WaitForAsync(() => workspace.Tools.TryGetPrimitive(name: "test", primitive: out _), what: "the tools to appear");

        await SampleRepository.AddProjectAsync(repository: this._repository, name: "Example.New.Tests", cancellationToken: this.CancellationToken());
        await this.WaitForAsync(() => workspace.Current.Repository?.UnitTestProjects.Count == 3, what: "the new project to be detected");

        File.Delete(this._repository.Combine("src/Example.slnx"));
        await this.WaitForAsync(() => workspace.Tools.IsEmpty, what: "the tools to go");
    }

    [Fact]
    public async Task AMissingRepositoryIsNotWatchedAsync()
    {
        using DotnetWorkspace workspace = await this.CreateWorkspaceAsync();
        await using RepositoryWatcher watcher = new(repositoryRoot: this._repository.Combine("missing"), source: workspace, timeProvider: TimeProvider.System, debounce: Debounce);

        Assert.False(condition: watcher.Start(), userMessage: "nothing to watch");
        Assert.False(condition: watcher.IsWatching, userMessage: "nothing is watched");
    }

    [Theory]
    [InlineData("src/Example.Core/obj/project.assets.csproj", true)]
    [InlineData("src/Example.Core/bin/Release/Example.Core.csproj", true)]
    [InlineData(".git/Example.slnx", true)]
    [InlineData("src/Example.Core/Example.Core.csproj", false)]
    [InlineData("src/Example.slnx", false)]
    [InlineData("Example.bin.csproj", false)]
    public async Task BuildOutputAndGitInternalsAreIgnoredAsync(string relative, bool ignored)
    {
        using DotnetWorkspace workspace = await this.CreateWorkspaceAsync();
        await using RepositoryWatcher watcher = new(repositoryRoot: this._repository.Path, source: workspace, timeProvider: TimeProvider.System, debounce: Debounce);

        Assert.Equal(expected: ignored, actual: watcher.IsIgnored(this._repository.Combine(relative)));
    }

    [Theory]
    [InlineData("src/Example.Core/Example.Core.csproj", false, true)]
    [InlineData("src/Example.slnx", false, true)]
    [InlineData("src/Example.sln", false, true)]
    [InlineData(".ai/commands.json", false, true)]
    [InlineData("src/Example.Core/Program.cs", false, false)]
    [InlineData("src/Example.Core", true, true)]
    [InlineData("src/Example.Core/obj", true, false)]
    [InlineData("src/Example.Core/obj/Example.Core.csproj.nuget.g.props", false, false)]
    public async Task OnlyChangesThatCanAffectDetectionCountAsync(string relative, bool mayBeDirectory, bool relevant)
    {
        using DotnetWorkspace workspace = await this.CreateWorkspaceAsync();
        await using RepositoryWatcher watcher = new(repositoryRoot: this._repository.Path, source: workspace, timeProvider: TimeProvider.System, debounce: Debounce);

        Assert.Equal(expected: relevant, actual: watcher.IsRelevant(fullPath: this._repository.Combine(relative), mayBeDirectory: mayBeDirectory));
    }

    private async ValueTask<DotnetWorkspace> CreateWorkspaceAsync()
    {
        DotnetDetection detection = await DotnetDetector.DetectAsync(repositoryRoot: this._repository.Path, cancellationToken: this.CancellationToken());

        return new(initial: detection, runner: new RecordingJobRunner(), buildtest: TestContexts.NoBuildtest);
    }

    private async ValueTask WaitForAsync(Func<bool> condition, string what)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationToken());
        timeout.CancelAfter(Patience);

        while (!condition())
        {
            try
            {
                await Task.Delay(delay: TimeSpan.FromMilliseconds(50), cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("Timed out waiting for " + what);
            }
        }
    }
}
