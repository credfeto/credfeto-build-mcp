using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Server;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Startup;

public sealed class CommandLineTests : TestBase
{
    private const int SERVED = 42;

    private static readonly BuildServerEnvironment VariableEnvironment = new(
        RepositoryRoot: "/work/checkout",
        RepositoryRootSource: RepositoryRootSource.Variable,
        LogDirectory: "/work/logs"
    );

    [Fact]
    public async Task NoArgumentsServesAndLogsTheRootUnderThePythonServerNameAsync()
    {
        (int exitCode, string output, string error, bool served) = await this.RunAsync();

        Assert.Equal(expected: SERVED, actual: exitCode);
        Assert.True(condition: served, userMessage: "the server is started");
        Assert.Equal(expected: string.Empty, actual: output);
        Assert.StartsWith(
            expectedStartString: "credfeto-build-python: repository root /work/checkout",
            actualString: error,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task HelpPrintsTheUsageAsync()
    {
        (int exitCode, string output, _, bool served) = await this.RunAsync(ServerCommandLine.HELP);

        Assert.Equal(expected: ServerCommandLine.SUCCESS, actual: exitCode);
        Assert.Equal(expected: CommandLine.Usage + Environment.NewLine, actual: output);
        Assert.False(condition: served, userMessage: "help does not serve");
    }

    [Fact]
    public async Task SelfTestPassesWithoutServingAsync()
    {
        (int exitCode, string output, string error, bool served) = await this.RunAsync(ServerCommandLine.SELF_TEST);

        Assert.Equal(expected: SelfTestRunner.PASSED, actual: exitCode);
        Assert.Equal(expected: string.Empty, actual: output);
        Assert.Equal(expected: string.Empty, actual: error);
        Assert.False(condition: served, userMessage: "the self-test does not serve");
    }

    private async ValueTask<(int ExitCode, string Output, string Error, bool Served)> RunAsync(
        params string[] arguments
    )
    {
        await using StringWriter output = new();
        await using StringWriter error = new();
        bool served = false;

        int exitCode = await CommandLine.RunAsync(
            arguments: arguments,
            output: output,
            error: error,
            resolveEnvironment: static _ => ValueTask.FromResult(VariableEnvironment),
            serve: (_, _) =>
            {
                served = true;

                return ValueTask.FromResult(SERVED);
            },
            cancellationToken: this.CancellationToken()
        );

        return (exitCode, output.ToString(), error.ToString(), served);
    }
}
