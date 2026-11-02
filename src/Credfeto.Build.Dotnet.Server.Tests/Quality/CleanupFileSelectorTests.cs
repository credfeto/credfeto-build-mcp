using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Quality;

/// <summary>Against a real git repository: a main branch, then a feature branch with changes of every kind.</summary>
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
        CancellationToken cancellationToken = this.CancellationToken();
        await TestGit.RunAsync(repository: this._repository.Path, arguments: ["init", "--quiet"], cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/Unchanged.cs", content: "class Unchanged {}\n", cancellationToken: cancellationToken);
        await TestGit.CommitAllAsync(repository: this._repository.Path, message: "base", cancellationToken: cancellationToken);

        CleanupSelection selection = await CleanupFileSelector.SelectAsync(
            repositoryRoot: this._repository.Path,
            scope: CleanupFileSelector.SCOPE_CHANGED,
            cancellationToken: cancellationToken
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
    public static void BuildOutputAndOtherFilesAreNeverSelected()
    {
        Assert.Empty(CleanupFileSelector.Filter(repositoryRoot: "/no/such/repo", relativePaths: ["src/obj/Generated.cs", "src/bin/X.cs", "README.md", "../outside.cs"]));
    }

    private async ValueTask CreateFeatureBranchAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        string root = this._repository.Path;

        await TestGit.RunAsync(repository: root, arguments: ["init", "--quiet"], cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: ".gitignore", content: "obj/\n", cancellationToken: cancellationToken);
        foreach (string name in new[] { "Unchanged", "Committed", "Staged", "Unstaged" })
        {
            _ = await this._repository.WriteAsync(relative: "src/" + name + ".cs", content: "class " + name + " {}\n", cancellationToken: cancellationToken);
        }

        _ = await this._repository.WriteAsync(relative: "README.md", content: "readme\n", cancellationToken: cancellationToken);
        await TestGit.CommitAllAsync(repository: root, message: "base", cancellationToken: cancellationToken);

        await TestGit.RunAsync(repository: root, arguments: ["checkout", "--quiet", "-b", "feature"], cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/Committed.cs", content: "class Committed { }\n", cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "README.md", content: "changed\n", cancellationToken: cancellationToken);
        await TestGit.CommitAllAsync(repository: root, message: "feature", cancellationToken: cancellationToken);

        _ = await this._repository.WriteAsync(relative: "src/Staged.cs", content: "class Staged { }\n", cancellationToken: cancellationToken);
        await TestGit.RunAsync(repository: root, arguments: ["add", "src/Staged.cs"], cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/Unstaged.cs", content: "class Unstaged { }\n", cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/New Untracked.cs", content: "class Untracked {}\n", cancellationToken: cancellationToken);
        _ = await this._repository.WriteAsync(relative: "src/obj/Generated.cs", content: "class Generated {}\n", cancellationToken: cancellationToken);
    }
}
