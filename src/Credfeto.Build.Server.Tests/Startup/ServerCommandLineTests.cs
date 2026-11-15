using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Configuration.Exceptions;
using Credfeto.Build.Server.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Build.Server.Tests.Startup;

public sealed class ServerCommandLineTests : TestBase
{
    private const string SERVER_NAME = "example-server";
    private const int SERVED = 42;

    private static readonly BuildServerEnvironment RepositoryEnvironment = new(
        RepositoryRoot: "/work/checkout",
        RepositoryRootSource: RepositoryRootSource.Variable,
        LogDirectory: "/work/logs"
    );

    [Fact]
    public async Task ServingNamesTheServerAndTheRepositoryRootOnStandardErrorAsync()
    {
        ServerCommandLine commandLine = Create(static _ => new ServiceCollection());

        (int exitCode, string output, string error, bool served) = await this.RunAsync(commandLine);

        Assert.Equal(expected: SERVED, actual: exitCode);
        Assert.True(condition: served, userMessage: "the server ran");
        Assert.Equal(expected: string.Empty, actual: output);
        Assert.Equal(
            expected: SERVER_NAME + ": " + RepositoryEnvironment.DescribeRepositoryRoot() + Environment.NewLine,
            actual: error
        );
    }

    [Fact]
    public async Task TheUsageNamesTheServerAsync()
    {
        ServerCommandLine commandLine = Create(static _ => new ServiceCollection());

        (int exitCode, string output, _, bool served) = await this.RunAsync(commandLine, ServerCommandLine.HELP);

        Assert.Equal(expected: ServerCommandLine.SUCCESS, actual: exitCode);
        Assert.False(condition: served, userMessage: "help does not serve");
        Assert.StartsWith(
            expectedStartString: "Usage: " + SERVER_NAME + " [" + ServerCommandLine.SELF_TEST,
            actualString: output,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Equal(expected: commandLine.Usage + Environment.NewLine, actual: output);
    }

    [Fact]
    public async Task TheSelfTestResolvesTheServersOwnServicesAsync()
    {
        BuildServerEnvironment? resolvedFor = null;
        ServerCommandLine commandLine = Create(environment =>
        {
            resolvedFor = environment;

            return new ServiceCollection().AddSingleton<FakeTool>();
        });

        (int exitCode, _, string error, bool served) = await this.RunAsync(commandLine, ServerCommandLine.SELF_TEST);

        Assert.Equal(expected: SelfTestRunner.FAILED, actual: exitCode);
        Assert.False(condition: served, userMessage: "the self-test does not serve");
        Assert.Same(expected: RepositoryEnvironment, actual: resolvedFor);
        Assert.StartsWith(
            expectedStartString: SelfTestRunner.FAILURE_PREFIX,
            actualString: error,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AnUnknownSwitchIsAUsageErrorAsync()
    {
        ServerCommandLine commandLine = Create(static _ => new ServiceCollection());

        (int exitCode, _, string error, bool served) = await this.RunAsync(commandLine, "--unknown");

        Assert.Equal(expected: ServerCommandLine.USAGE_ERROR, actual: exitCode);
        Assert.False(condition: served, userMessage: "an unknown switch does not serve");
        Assert.Equal(
            expected: "Unrecognised arguments: --unknown (see " + ServerCommandLine.HELP + ")" + Environment.NewLine,
            actual: error
        );
    }

    [Fact]
    public async Task AConfigurationErrorIsReportedWithoutServingAsync()
    {
        ServerCommandLine commandLine = Create(static _ => new ServiceCollection());
        await using StringWriter output = new();
        await using StringWriter error = new();

        int exitCode = await commandLine.RunAsync(
            arguments: [],
            output: output,
            error: error,
            resolveEnvironment: static _ => throw new InvalidBuildConfigurationException("bad root"),
            serve: static (_, _) => ValueTask.FromResult(SERVED),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: ServerCommandLine.CONFIGURATION_ERROR, actual: exitCode);
        Assert.Equal(expected: "bad root" + Environment.NewLine, actual: error.ToString());
    }

    private static ServerCommandLine Create(Func<BuildServerEnvironment, IServiceCollection> selfTestServices)
    {
        return new(
            serverName: SERVER_NAME,
            selfTestServices: selfTestServices,
            buildServiceProvider: ValidatingProvider.Build
        );
    }

    private async ValueTask<(int ExitCode, string Output, string Error, bool Served)> RunAsync(
        ServerCommandLine commandLine,
        params string[] arguments
    )
    {
        await using StringWriter output = new();
        await using StringWriter error = new();
        bool served = false;

        int exitCode = await commandLine.RunAsync(
            arguments: arguments,
            output: output,
            error: error,
            resolveEnvironment: static _ => ValueTask.FromResult(RepositoryEnvironment),
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
