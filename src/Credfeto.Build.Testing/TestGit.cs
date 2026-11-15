using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Testing;

// Drives git in a temporary repository, isolated from the user's hooks, signing and identity
public static class TestGit
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

    public static async ValueTask RunAsync(
        string repository,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> all = [.. Isolation, .. arguments];
        _ = await GitCommand.TryRunAsync(
            repositoryRoot: repository,
            arguments: all,
            cancellationToken: cancellationToken
        ) ?? throw new InvalidOperationException("git " + string.Join(separator: ' ', values: arguments) + " failed");
    }

    public static async ValueTask CommitAllAsync(string repository, string message, CancellationToken cancellationToken)
    {
        await RunAsync(repository: repository, arguments: ["add", "--all"], cancellationToken: cancellationToken);
        await RunAsync(
            repository: repository,
            arguments: ["commit", "--quiet", "--no-verify", "-m", message],
            cancellationToken: cancellationToken
        );
    }
}
