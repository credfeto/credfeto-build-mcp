using System;
using Credfeto.Build.Core;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Workspace;
using Credfeto.Build.Server.Commands;
using Credfeto.Build.Server.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Python.Server;

public static class ServerSetup
{
    public static IServiceCollection AddPythonServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        PythonDetection detection
    )
    {
        return services.AddPythonServer(
            environment: environment,
            detection: detection,
            python: PythonLocator.FromProcess(),
            debounce: RepositoryWatcher.DefaultDebounce
        );
    }

    public static IServiceCollection AddPythonServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        PythonDetection detection,
        PythonLocator python,
        TimeSpan debounce
    )
    {
        return services
            .AddBuildCore(environment)
            .AddSingleton(python)
            .AddSingleton<ICommandRunner, ProcessCommandRunner>()
            .AddSingleton(provider => new PythonWorkspace(
                initial: detection,
                runner: provider.GetRequiredService<IJobRunner>(),
                commands: provider.GetRequiredService<ICommandRunner>(),
                python: provider.GetRequiredService<PythonLocator>()
            ))
            .AddSingleton(provider => new RepositoryWatcher(
                repositoryRoot: environment.RepositoryRoot,
                source: provider.GetRequiredService<PythonWorkspace>(),
                filter: PythonWorkspace.WatchFilterFor(detection),
                timeProvider: provider.GetRequiredService<TimeProvider>(),
                debounce: debounce
            ))
            .AddSingleton(static provider =>
                ServerOptionsFactory.Create(provider.GetRequiredService<PythonWorkspace>())
            );
    }
}
