using System;
using Credfeto.Build.Core;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Workspace;
using Credfeto.Build.Server.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Node.Server;

public static class ServerSetup
{
    public static IServiceCollection AddNodeServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        NodeDetection detection
    )
    {
        return services.AddNodeServer(
            environment: environment,
            detection: detection,
            debounce: RepositoryWatcher.DefaultDebounce
        );
    }

    public static IServiceCollection AddNodeServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        NodeDetection detection,
        TimeSpan debounce
    )
    {
        return services
            .AddBuildCore(environment)
            .AddSingleton(_ => new NodeWorkspace(detection))
            .AddSingleton(provider => new RepositoryWatcher(
                repositoryRoot: environment.RepositoryRoot,
                source: provider.GetRequiredService<NodeWorkspace>(),
                filter: NodeWorkspace.WatchFilterFor(detection),
                timeProvider: provider.GetRequiredService<TimeProvider>(),
                debounce: debounce
            ))
            .AddSingleton(static provider => ServerOptionsFactory.Create(provider.GetRequiredService<NodeWorkspace>()));
    }
}
