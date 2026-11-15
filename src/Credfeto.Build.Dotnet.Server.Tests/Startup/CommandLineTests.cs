using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Configuration.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Startup;

public sealed class CommandLineTests : TestBase
{
    private const int SERVED = 42;

    private static readonly BuildServerEnvironment GitRootEnvironment = new(
        RepositoryRoot: "/work/checkout",
        RepositoryRootSource: RepositoryRootSource.GitRoot,
        LogDirectory: "/work/logs"
    );

    private static readonly string RootLine =
        "credfeto-build-dotnet: repository root /work/checkout (the git root of the current directory; BUILD_MCP_REPO_ROOT is not set and the default folder does not exist)"
        + Environment.NewLine;

    [Fact]
    public async Task NoArgumentsServesWithTheCallersTokenAsync()
    {
        CancellationToken expected = this.CancellationToken();

        (int exitCode, _, _, CancellationToken? served) = await this.RunAsync();

        Assert.Equal(expected: SERVED, actual: exitCode);
        Assert.Equal(expected: expected, actual: served);
    }

    [Fact]
    public async Task ServingLogsTheRepositoryRootOnStandardErrorOnlyAsync()
    {
        (_, string output, string error, _) = await this.RunAsync();

        Assert.Equal(expected: string.Empty, actual: output);
        Assert.Equal(expected: RootLine, actual: error);
    }

    [Theory]
    [InlineData(CommandLine.HELP)]
    [InlineData(CommandLine.SHORT_HELP)]
    public async Task HelpListsTheSwitchesAsync(string argument)
    {
        (int exitCode, string output, string error, CancellationToken? served) = await this.RunAsync(argument);

        Assert.Equal(expected: CommandLine.SUCCESS, actual: exitCode);
        Assert.Equal(expected: CommandLine.Usage + Environment.NewLine, actual: output);
        Assert.Contains(
            expectedSubstring: CommandLine.SELF_TEST,
            actualString: output,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: CommandLine.HELP,
            actualString: output,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Equal(expected: string.Empty, actual: error);
        Assert.Null(served);
    }

    [Fact]
    public async Task SelfTestPassesWithoutServingAsync()
    {
        (int exitCode, string output, string error, CancellationToken? served) = await this.RunAsync(
            CommandLine.SELF_TEST
        );

        Assert.Equal(expected: SelfTest.PASSED, actual: exitCode);
        Assert.Equal(expected: string.Empty, actual: output);
        Assert.Equal(expected: string.Empty, actual: error);
        Assert.Null(served);
    }

    [Fact]
    public Task AnUnknownSwitchIsAUsageErrorAsync()
    {
        return this.AssertUsageErrorAsync("--unknown");
    }

    [Fact]
    public Task MoreThanOneSwitchIsAUsageErrorAsync()
    {
        return this.AssertUsageErrorAsync(CommandLine.SELF_TEST, CommandLine.HELP);
    }

    [Theory]
    [InlineData("")]
    [InlineData(CommandLine.SELF_TEST)]
    public async Task AConfigurationErrorIsReportedOnStandardErrorWithoutServingAsync(string argument)
    {
        string[] arguments = argument.Length == 0 ? [] : [argument];
        await using StringWriter output = new();
        await using StringWriter error = new();
        bool served = false;

        int exitCode = await CommandLine.RunAsync(
            arguments: arguments,
            output: output,
            error: error,
            resolveEnvironment: static _ =>
                throw new InvalidBuildConfigurationException("BUILD_MCP_REPO_ROOT is set to \"repo\""),
            serve: (_, _) =>
            {
                served = true;

                return ValueTask.FromResult(SERVED);
            },
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: CommandLine.CONFIGURATION_ERROR, actual: exitCode);
        Assert.False(condition: served, userMessage: "The server should not start");
        Assert.Equal(expected: string.Empty, actual: output.ToString());
        Assert.Equal(
            expected: "BUILD_MCP_REPO_ROOT is set to \"repo\"" + Environment.NewLine,
            actual: error.ToString()
        );
    }

    private async Task AssertUsageErrorAsync(params string[] arguments)
    {
        (int exitCode, string output, string error, CancellationToken? served) = await this.RunAsync(arguments);

        Assert.Equal(expected: CommandLine.USAGE_ERROR, actual: exitCode);
        Assert.Equal(expected: string.Empty, actual: output);
        Assert.Equal(
            expected: "Unrecognised arguments: "
                + string.Join(separator: ' ', values: arguments)
                + " (see "
                + CommandLine.HELP
                + ")"
                + Environment.NewLine,
            actual: error
        );
        Assert.Null(served);
    }

    private async ValueTask<(int ExitCode, string Output, string Error, CancellationToken? Served)> RunAsync(
        params string[] arguments
    )
    {
        await using StringWriter output = new();
        await using StringWriter error = new();
        CancellationToken? served = null;

        int exitCode = await CommandLine.RunAsync(
            arguments: arguments,
            output: output,
            error: error,
            resolveEnvironment: static _ => ValueTask.FromResult(GitRootEnvironment),
            serve: (environment, cancellationToken) =>
            {
                Assert.Same(expected: GitRootEnvironment, actual: environment);
                served = cancellationToken;

                return ValueTask.FromResult(SERVED);
            },
            cancellationToken: this.CancellationToken()
        );

        return (exitCode, output.ToString(), error.ToString(), served);
    }
}
