using System;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Resources;
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
        Assert.Equal(
            expected: "/work/repo",
            actual: this.ServiceProvider.GetRequiredService<BuildServerEnvironment>().RepositoryRoot
        );
    }

    [Fact]
    public void TheMachinesResourcesAreRegistered()
    {
        Assert.IsType<SystemMemorySource>(this.ServiceProvider.GetRequiredService<IMemorySource>());

        // The host's own memory limit decides the count, so only its bounds are certain
        ResourceNote plan = this
            .ServiceProvider.GetRequiredService<MachineResources>()
            .Plan(WorkerBudget.TEST_MEMORY_PER_WORKER_MB);

        Assert.InRange(actual: plan.Workers, low: 1, high: Environment.ProcessorCount);
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddBuildCore(
            new(
                RepositoryRoot: "/work/repo",
                RepositoryRootSource: RepositoryRootSource.Variable,
                LogDirectory: "/work/logs"
            )
        );
    }
}
