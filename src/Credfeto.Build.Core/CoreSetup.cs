using System;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Core;

public static class CoreSetup
{
    public static IServiceCollection AddBuildCore(this IServiceCollection services, BuildServerEnvironment environment)
    {
        return services
            .AddSingleton(TimeProvider.System)
            .AddSingleton(environment)
            .AddSingleton(JobRunnerSettings.Create(environment.LogDirectory))
            .AddSingleton<IOutputMasker>(new OutputMasker(BuildServerEnvironment.SnapshotVariables()))
            .AddSingleton<IProcessLauncher, SystemProcessLauncher>()
            .AddSingleton<IJobRunner, JobRunner>();
    }
}
