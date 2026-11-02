using System;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class GitCommandTests : TestBase
{
    [Fact]
    public static void NulSeparatedOutputIsSplitIntoEntries()
    {
        Assert.Equal(expected: ["a.cs", "dir/b c.cs"], actual: GitCommand.SplitNul("a.cs\0dir/b c.cs\0"), comparer: StringComparer.Ordinal);
        Assert.Empty(GitCommand.SplitNul(null));
    }

    [Fact]
    public async Task OutputIsReturnedOnSuccessAsync()
    {
        using TemporaryDirectory repository = new();

        Assert.NotNull(await GitCommand.TryRunAsync(repositoryRoot: repository.Path, arguments: ["init", "--quiet"], cancellationToken: this.CancellationToken()));
        string? head = await GitCommand.TryRunAsync(repositoryRoot: repository.Path, arguments: ["rev-parse", "--is-inside-work-tree"], cancellationToken: this.CancellationToken());

        Assert.Equal(expected: "true", actual: head?.Trim());
    }

    [Fact]
    public async Task FailureIsNullAsync()
    {
        using TemporaryDirectory directory = new();

        string? result = await GitCommand.TryRunAsync(repositoryRoot: directory.Path, arguments: ["rev-parse", "--verify", "--quiet", "no-such-branch"], cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }
}
