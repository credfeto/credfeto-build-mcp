using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Quality;

public sealed class CleanupFileSelectorTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task ChangedIsEveryCsFileChangedSinceTheMergeBaseAsync()
    {
        await this.CreateFeatureBranchAsync();

        CleanupSelection selection = await CleanupFileSelector.SelectAsync(
            repositoryRoot: this._repository.Path,
            scope: CleanupFileSelector.SCOPE_CHANGED,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(selection.Error);
        Assert.Equal(expected: "main", actual: selection.BaseReference);
        Assert.Equal(
            expected:
            [
                this._repository.Combine("src/Committed.cs"),
                this._repository.Combine("src/New Untracked.cs"),
                this._repository.Combine("src/Staged.cs"),
                this._repository.Combine("src/Unstaged.cs"),
            ],
            actual: selection.Files,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task AllIsEveryTrackedCsFileAsync()
    {
        await this.CreateFeatureBranchAsync();

        CleanupSelection selection = await CleanupFileSelector.SelectAsync(
            repositoryRoot: this._repository.Path,
            scope: CleanupFileSelector.SCOPE_ALL,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(selection.Error);
        Assert.Equal(
            expected:
            [
                this._repository.Combine("src/Committed.cs"),
                this._repository.Combine("src/Staged.cs"),
                this._repository.Combine("src/Unchanged.cs"),
                this._repository.Combine("src/Unstaged.cs"),
            ],
            actual: selection.Files,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task OnTheDefaultBranchWithNoChangesNothingIsSelectedAsync()
    {
        await this.CreateSingleCommitAsync();

        CleanupSelection selection = await CleanupFileSelector.SelectAsync(
            repositoryRoot: this._repository.Path,
            scope: CleanupFileSelector.SCOPE_CHANGED,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(selection.Error);
        Assert.Empty(selection.Files);
    }

    [Fact]
    public async Task NotAGitRepositoryIsAnErrorAsync()
    {
        CleanupSelection selection = await CleanupFileSelector.SelectAsync(
            repositoryRoot: this._repository.Path,
            scope: CleanupFileSelector.SCOPE_ALL,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(selection.Error);
        Assert.Empty(selection.Files);
    }

    [Fact]
    public async Task AMergeBaseGitCannotResolveListsNothingAsync()
    {
        await this.CreateSingleCommitAsync();

        IReadOnlyList<string>? paths = await CleanupFileSelector.ListChangedPathsAsync(
            repositoryRoot: this._repository.Path,
            mergeBase: "no-such-revision",
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(paths);
    }

    [Fact]
    public static void BuildOutputAndOtherFilesAreNeverSelected()
    {
        Assert.Empty(
            CleanupFileSelector.Filter(
                repositoryRoot: "/no/such/repo",
                relativePaths: ["src/obj/Generated.cs", "src/bin/X.cs", "README.md", "../outside.cs"]
            )
        );
    }

    private async ValueTask CreateFeatureBranchAsync()
    {
        await this.GitAsync("init", "--quiet");
        await this.WriteAsync(relative: ".gitignore", content: "obj/\n");
        foreach (string name in new[] { "Unchanged", "Committed", "Staged", "Unstaged" })
        {
            await this.WriteAsync(relative: "src/" + name + ".cs", content: "class " + name + " {}\n");
        }

        await this.WriteAsync(relative: "README.md", content: "readme\n");
        await this.CommitAsync("base");

        await this.GitAsync("checkout", "--quiet", "-b", "feature");
        await this.WriteAsync(relative: "src/Committed.cs", content: "class Committed { }\n");
        await this.WriteAsync(relative: "README.md", content: "changed\n");
        await this.CommitAsync("feature");

        await this.ChangeWorkingTreeAsync();
    }

    private async ValueTask ChangeWorkingTreeAsync()
    {
        await this.WriteAsync(relative: "src/Staged.cs", content: "class Staged { }\n");
        await this.GitAsync("add", "src/Staged.cs");
        await this.WriteAsync(relative: "src/Unstaged.cs", content: "class Unstaged { }\n");
        await this.WriteAsync(relative: "src/New Untracked.cs", content: "class Untracked {}\n");
        await this.WriteAsync(relative: "src/obj/Generated.cs", content: "class Generated {}\n");
    }

    private async ValueTask CreateSingleCommitAsync()
    {
        await this.GitAsync("init", "--quiet");
        await this.WriteAsync(relative: "src/Unchanged.cs", content: "class Unchanged {}\n");
        await this.CommitAsync("base");
    }

    private async ValueTask WriteAsync(string relative, string content)
    {
        _ = await this._repository.WriteAsync(
            relative: relative,
            content: content,
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask GitAsync(params string[] arguments)
    {
        return TestGit.RunAsync(
            repository: this._repository.Path,
            arguments: arguments,
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask CommitAsync(string message)
    {
        return TestGit.CommitAllAsync(
            repository: this._repository.Path,
            message: message,
            cancellationToken: this.CancellationToken()
        );
    }
}
