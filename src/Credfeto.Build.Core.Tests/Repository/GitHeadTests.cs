using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class GitHeadTests : TestBase
{
    private const string SHA = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task DetachedHeadIsReadAsync()
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: ".git/HEAD", content: SHA + "\n", cancellationToken: this.CancellationToken());

        Assert.Equal(expected: SHA, actual: await GitHead.TryReadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken()));
    }

    [Fact]
    public async Task LooseBranchReferenceIsResolvedAsync()
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: ".git/HEAD", content: "ref: refs/heads/main\n", cancellationToken: this.CancellationToken());
        await repository.WriteAsync(relative: ".git/refs/heads/main", content: SHA + "\n", cancellationToken: this.CancellationToken());

        Assert.Equal(expected: SHA, actual: await GitHead.TryReadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken()));
    }

    [Fact]
    public async Task PackedReferenceIsResolvedAsync()
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: ".git/HEAD", content: "ref: refs/heads/main\n", cancellationToken: this.CancellationToken());
        await repository.WriteAsync(relative: ".git/packed-refs", content: "# pack-refs with: peeled fully-peeled sorted\n" + SHA + " refs/heads/main\n", cancellationToken: this.CancellationToken());

        Assert.Equal(expected: SHA, actual: await GitHead.TryReadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken()));
    }

    [Fact]
    public async Task WorktreeGitFileIsFollowedAsync()
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: "main/.git/refs/heads/feature", content: SHA + "\n", cancellationToken: this.CancellationToken());
        await repository.WriteAsync(relative: "main/.git/worktrees/wt/HEAD", content: "ref: refs/heads/feature\n", cancellationToken: this.CancellationToken());
        await repository.WriteAsync(relative: "main/.git/worktrees/wt/commondir", content: "../..\n", cancellationToken: this.CancellationToken());
        await repository.WriteAsync(relative: "wt/.git", content: "gitdir: " + repository.Combine("main/.git/worktrees/wt") + "\n", cancellationToken: this.CancellationToken());

        Assert.Equal(expected: SHA, actual: await GitHead.TryReadAsync(repositoryRoot: repository.Combine("wt"), cancellationToken: this.CancellationToken()));
    }

    [Theory]
    [InlineData("ref: refs/../../../etc/passwd")]
    [InlineData("ref: heads/main")]
    [InlineData("not-a-sha")]
    public async Task UnsafeOrInvalidHeadGivesNoShaAsync(string head)
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: ".git/HEAD", content: head + "\n", cancellationToken: this.CancellationToken());

        Assert.Null(await GitHead.TryReadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken()));
    }

    [Fact]
    public async Task NoGitDirectoryGivesNoShaAsync()
    {
        using TemporaryDirectory repository = new();

        Assert.Null(await GitHead.TryReadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken()));
    }
}
