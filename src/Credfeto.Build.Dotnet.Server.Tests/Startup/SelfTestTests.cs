using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tools;
using Credfeto.Build.Dotnet.Server.Workspace;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Startup;

public sealed class SelfTestTests : TestBase
{
    [Fact]
    public async Task TheServerResolvesWithoutTouchingTheRepositoryOrLogsAsync()
    {
        using TemporaryDirectory folder = new();
        BuildServerEnvironment environment = new(
            RepositoryRoot: folder.Combine("repo"),
            LogDirectory: folder.Combine("logs")
        );
        await using StringWriter error = new();

        int exitCode = await SelfTest.RunAsync(
            services: SelfTest.Services(environment),
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTest.PASSED, actual: exitCode);
        Assert.Equal(expected: string.Empty, actual: error.ToString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder.Path));
    }

    [Fact]
    public async Task EveryToolIsBuiltFromTheMadeUpRepositoryAsync()
    {
        using TemporaryDirectory folder = new();
        BuildServerEnvironment environment = new(
            RepositoryRoot: folder.Combine("repo"),
            LogDirectory: folder.Combine("logs")
        );

        await using ServiceProvider provider = SelfTest.Services(environment).BuildServiceProvider();
        DotnetWorkspace workspace = provider.GetRequiredService<DotnetWorkspace>();

        Assert.Equal(expected: 11, actual: workspace.Tools.Count);
    }

    [Fact]
    public async Task ADependencyThatCannotBeResolvedFailsWithOneLineAsync()
    {
        IServiceCollection services = new ServiceCollection().AddSingleton<ToolContext>();
        await using StringWriter error = new();

        int exitCode = await SelfTest.RunAsync(
            services: services,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTest.FAILED, actual: exitCode);
        AssertOneFailureLine(error.ToString());
    }

    [Fact]
    public async Task AFactoryThatThrowsReportsItsMessageAsync()
    {
        IServiceCollection services = new ServiceCollection().AddSingleton<ToolContext>(static _ =>
            throw new InvalidOperationException("first line" + Environment.NewLine + "second line")
        );
        await using StringWriter error = new();

        int exitCode = await SelfTest.RunAsync(
            services: services,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTest.FAILED, actual: exitCode);
        Assert.Equal(
            expected: SelfTest.FAILURE_PREFIX + "first line second line" + Environment.NewLine,
            actual: error.ToString()
        );
    }

    private static void AssertOneFailureLine(string written)
    {
        Assert.StartsWith(
            expectedStartString: SelfTest.FAILURE_PREFIX,
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
