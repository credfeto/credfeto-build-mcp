using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Startup;

public sealed class CommandLineTests : TestBase
{
    private const int SERVED = 42;

    [Fact]
    public async Task NoArgumentsServesWithTheCallersTokenAsync()
    {
        CancellationToken expected = this.CancellationToken();

        (int exitCode, string output, string error, CancellationToken? served) = await this.RunAsync();

        Assert.Equal(expected: SERVED, actual: exitCode);
        Assert.Equal(expected: expected, actual: served);
        Assert.Equal(expected: string.Empty, actual: output);
        Assert.Equal(expected: string.Empty, actual: error);
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
            serve: cancellationToken =>
            {
                served = cancellationToken;

                return ValueTask.FromResult(SERVED);
            },
            cancellationToken: this.CancellationToken()
        );

        return (exitCode, output.ToString(), error.ToString(), served);
    }
}
