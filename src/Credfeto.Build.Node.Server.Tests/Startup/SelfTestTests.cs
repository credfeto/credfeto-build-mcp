using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Node.Server.Workspace;
using Credfeto.Build.Server;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Startup;

public sealed class SelfTestTests : TestBase
{
    [Fact]
    public async Task TheServerResolvesWithoutTouchingTheRepositoryOrLogsAsync()
    {
        using TemporaryDirectory folder = new();
        await using StringWriter error = new();

        int exitCode = await SelfTestRunner.RunAsync(
            services: SelfTest.Services(Environment(folder)),
            buildServiceProvider: SelfTest.BuildServiceProvider,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTestRunner.PASSED, actual: exitCode);
        Assert.Equal(expected: string.Empty, actual: error.ToString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder.Path));
    }

    [Fact]
    public async Task EveryToolIsBuiltFromTheMadeUpRepositoryAsync()
    {
        using TemporaryDirectory folder = new();

        await using ServiceProvider provider = SelfTest.Services(Environment(folder)).BuildServiceProvider();
        NodeWorkspace workspace = provider.GetRequiredService<NodeWorkspace>();

        Assert.Single(workspace.Tools);
    }

    private static BuildServerEnvironment Environment(TemporaryDirectory folder)
    {
        return new(
            RepositoryRoot: folder.Combine("repo"),
            RepositoryRootSource: RepositoryRootSource.Variable,
            LogDirectory: folder.Combine("logs")
        );
    }
}
