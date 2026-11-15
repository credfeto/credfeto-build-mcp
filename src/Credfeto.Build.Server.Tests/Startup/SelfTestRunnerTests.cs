using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Build.Server.Tests.Startup;

public sealed class SelfTestRunnerTests : TestBase
{
    [Fact]
    public async Task ServicesThatResolvePassSilentlyAsync()
    {
        IServiceCollection services = new ServiceCollection().AddSingleton(new FakeTool("detect"));
        await using StringWriter error = new();

        int exitCode = await SelfTestRunner.RunAsync(
            services: services,
            buildServiceProvider: ValidatingProvider.Build,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTestRunner.PASSED, actual: exitCode);
        Assert.Equal(expected: string.Empty, actual: error.ToString());
    }

    [Fact]
    public async Task AProviderThatOwnsNothingIsNotDisposedAsync()
    {
        await using StringWriter error = new();

        int exitCode = await SelfTestRunner.RunAsync(
            services: new ServiceCollection(),
            buildServiceProvider: static _ => new NothingOwnedServiceProvider(),
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTestRunner.PASSED, actual: exitCode);
    }

    [Fact]
    public async Task ADependencyThatCannotBeResolvedFailsWithOneLineAsync()
    {
        IServiceCollection services = new ServiceCollection().AddSingleton<FakeTool>();
        await using StringWriter error = new();

        int exitCode = await SelfTestRunner.RunAsync(
            services: services,
            buildServiceProvider: ValidatingProvider.Build,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTestRunner.FAILED, actual: exitCode);
        AssertOneFailureLine(error.ToString());
    }

    [Fact]
    public async Task AFactoryThatThrowsReportsItsMessageAsync()
    {
        IServiceCollection services = new ServiceCollection().AddSingleton<FakeTool>(static _ =>
            throw new InvalidOperationException("first line" + Environment.NewLine + "second line")
        );
        await using StringWriter error = new();

        int exitCode = await SelfTestRunner.RunAsync(
            services: services,
            buildServiceProvider: ValidatingProvider.Build,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTestRunner.FAILED, actual: exitCode);
        Assert.Equal(
            expected: SelfTestRunner.FAILURE_PREFIX + "first line second line" + Environment.NewLine,
            actual: error.ToString()
        );
    }

    private static void AssertOneFailureLine(string written)
    {
        Assert.StartsWith(
            expectedStartString: SelfTestRunner.FAILURE_PREFIX,
            actualString: written,
            comparisonType: StringComparison.Ordinal
        );
        Assert.EndsWith(
            expectedEndString: Environment.NewLine,
            actualString: written,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Equal(
            expected: written.Length - Environment.NewLine.Length,
            actual: written.IndexOf(value: Environment.NewLine, comparisonType: StringComparison.Ordinal)
        );
    }
}
