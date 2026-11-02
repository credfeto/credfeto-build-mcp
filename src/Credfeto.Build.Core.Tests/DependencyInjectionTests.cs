using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Build.Core.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void JobRunnerAndItsDependenciesAreRegistered()
    {
        Assert.NotNull(this.ServiceProvider.GetRequiredService<IJobRunner>());
        Assert.NotNull(this.ServiceProvider.GetRequiredService<IProcessLauncher>());
        Assert.NotNull(this.ServiceProvider.GetRequiredService<IOutputMasker>());
        Assert.Equal(expected: "/work/repo", actual: this.ServiceProvider.GetRequiredService<BuildServerEnvironment>().RepositoryRoot);
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddBuildCore(new(RepositoryRoot: "/work/repo", LogDirectory: "/work/logs"));
    }
}
