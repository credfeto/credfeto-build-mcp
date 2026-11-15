using System;
using Credfeto.Build.Core;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Workspace;
using Credfeto.Build.Server.Commands;
using Credfeto.Build.Server.Workspace;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerSetup
{
    public static IServiceCollection AddDotnetServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        DotnetDetection detection
    )
    {
        return services.AddDotnetServer(
            environment: environment,
            detection: detection,
            buildtest: BuildtestLocator.FromProcess(),
            debounce: RepositoryWatcher.DefaultDebounce
        );
    }

    public static IServiceCollection AddDotnetServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        DotnetDetection detection,
        BuildtestLocator buildtest,
        TimeSpan debounce
    )
    {
        return services
            .AddBuildCore(environment)
            .AddSingleton(buildtest)
            .AddSingleton<ICommandRunner, ProcessCommandRunner>()
            .AddSingleton(provider => new DotnetWorkspace(
                initial: detection,
                runner: provider.GetRequiredService<IJobRunner>(),
                commands: provider.GetRequiredService<ICommandRunner>(),
                buildtest: provider.GetRequiredService<BuildtestLocator>()
            ))
            .AddSingleton(provider => new RepositoryWatcher(
                repositoryRoot: environment.RepositoryRoot,
                source: provider.GetRequiredService<DotnetWorkspace>(),
                filter: DotnetWorkspace.WatchedFiles,
                timeProvider: provider.GetRequiredService<TimeProvider>(),
                debounce: debounce
            ))
            .AddSingleton(static provider =>
                ServerOptionsFactory.Create(provider.GetRequiredService<DotnetWorkspace>())
            );
    }
}
