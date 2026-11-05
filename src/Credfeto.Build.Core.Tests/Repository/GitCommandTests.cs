using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class GitCommandTests : TestBase
{
    private static readonly string[] Isolation =
    [
        "-c",
        "user.name=Test",
        "-c",
        "user.email=test@example.com",
        "-c",
        "commit.gpgsign=false",
        "-c",
        "core.hooksPath=/dev/null",
        "-c",
        "init.defaultBranch=main",
    ];

    [Fact]
    public static void NulSeparatedOutputIsSplitIntoEntries()
    {
        Assert.Equal(
            expected: ["a.cs", "dir/b c.cs"],
            actual: GitCommand.SplitNul("a.cs\0dir/b c.cs\0"),
            comparer: StringComparer.Ordinal
        );
        Assert.Empty(GitCommand.SplitNul(null));
    }

    [Fact]
    public async Task OutputIsReturnedOnSuccessAsync()
    {
        using TemporaryDirectory repository = new();

        Assert.NotNull(
            await GitCommand.TryRunAsync(
                repositoryRoot: repository.Path,
                arguments: ["init", "--quiet"],
                cancellationToken: this.CancellationToken()
            )
        );
        string? head = await GitCommand.TryRunAsync(
            repositoryRoot: repository.Path,
            arguments: ["rev-parse", "--is-inside-work-tree"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "true", actual: head?.Trim());
    }

    [Fact]
    public async Task InheritedRepositoryVariablesDoNotRedirectGitAsync()
    {
        using TemporaryDirectory other = new();
        using TemporaryDirectory repository = new();

        await RunIsolatedAsync(
            repository: other.Path,
            arguments: ["init", "--quiet"],
            cancellationToken: this.CancellationToken()
        );

        string? originalGitDir = Environment.GetEnvironmentVariable("GIT_DIR");
        string? originalIndexFile = Environment.GetEnvironmentVariable("GIT_INDEX_FILE");

        try
        {
            // As git sets them for a hook running in another repository
            Environment.SetEnvironmentVariable(variable: "GIT_DIR", value: other.Combine(".git"));
            Environment.SetEnvironmentVariable(variable: "GIT_INDEX_FILE", value: other.Combine(".git/index"));

            await InitAndCommitAsync(repository: repository, cancellationToken: this.CancellationToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "GIT_DIR", value: originalGitDir);
            Environment.SetEnvironmentVariable(variable: "GIT_INDEX_FILE", value: originalIndexFile);
        }

        Assert.NotNull(await HeadAsync(repository: repository.Path, cancellationToken: this.CancellationToken()));
        Assert.Null(await HeadAsync(repository: other.Path, cancellationToken: this.CancellationToken()));
        Assert.False(File.Exists(other.Combine(".git/index")), userMessage: "The other repository's index was written");

        string? bare = await GitCommand.TryRunAsync(
            repositoryRoot: other.Path,
            arguments: ["config", "--get", "core.bare"],
            cancellationToken: this.CancellationToken()
        );
        Assert.Equal(expected: "false", actual: bare?.Trim());
    }

    [Fact]
    public async Task FailureIsNullAsync()
    {
        using TemporaryDirectory directory = new();

        string? result = await GitCommand.TryRunAsync(
            repositoryRoot: directory.Path,
            arguments: ["rev-parse", "--verify", "--quiet", "no-such-branch"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(result);
    }

    private static async ValueTask InitAndCommitAsync(
        TemporaryDirectory repository,
        CancellationToken cancellationToken
    )
    {
        await RunIsolatedAsync(
            repository: repository.Path,
            arguments: ["init", "--quiet"],
            cancellationToken: cancellationToken
        );
        _ = await repository.WriteAsync(relative: "file.txt", content: "content", cancellationToken: cancellationToken);
        await RunIsolatedAsync(
            repository: repository.Path,
            arguments: ["add", "--all"],
            cancellationToken: cancellationToken
        );
        await RunIsolatedAsync(
            repository: repository.Path,
            arguments: ["commit", "--quiet", "--no-verify", "-m", "Temp"],
            cancellationToken: cancellationToken
        );
    }

    private static ValueTask<string?> HeadAsync(string repository, CancellationToken cancellationToken)
    {
        return GitCommand.TryRunAsync(
            repositoryRoot: repository,
            arguments: ["rev-parse", "--verify", "--quiet", "HEAD"],
            cancellationToken: cancellationToken
        );
    }

    private static async ValueTask RunIsolatedAsync(
        string repository,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> all = [.. Isolation, .. arguments];
        string? output = await GitCommand.TryRunAsync(
            repositoryRoot: repository,
            arguments: all,
            cancellationToken: cancellationToken
        );

        Assert.True(
            condition: output is not null,
            userMessage: "git " + string.Join(separator: ' ', values: arguments) + " failed"
        );
    }
}
