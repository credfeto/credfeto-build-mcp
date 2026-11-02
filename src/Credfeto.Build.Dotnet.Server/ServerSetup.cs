using System;
using Credfeto.Build.Core;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Workspace;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerSetup
{
    /// <summary>
    ///     Registers the server for one repository. Start-up detection seeds the tool list; the repository watcher
    ///     and every tool call re-detect, and the tool list follows the repository for the rest of the session.
    /// </summary>
    public static IServiceCollection AddDotnetServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        DotnetDetection detection
    )
    {
        return services.AddDotnetServer(environment: environment, detection: detection, buildtest: BuildtestLocator.FromProcess(), debounce: RepositoryWatcher.DefaultDebounce);
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
            .AddSingleton(provider => new DotnetWorkspace(
                initial: detection,
                runner: provider.GetRequiredService<IJobRunner>(),
                buildtest: provider.GetRequiredService<BuildtestLocator>()
            ))
            .AddSingleton(provider => new RepositoryWatcher(
                repositoryRoot: environment.RepositoryRoot,
                source: provider.GetRequiredService<DotnetWorkspace>(),
                timeProvider: provider.GetRequiredService<TimeProvider>(),
                debounce: debounce
            ))
            .AddSingleton(static provider => ServerOptionsFactory.Create(provider.GetRequiredService<DotnetWorkspace>()));
    }
}
