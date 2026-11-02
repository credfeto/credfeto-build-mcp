using System.Collections.Generic;
using Credfeto.Build.Core;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerSetup
{
    /// <summary>
    ///     Registers the server for one repository. Detection has already run and is fixed for the session, so the
    ///     tool list never changes while the server is up.
    /// </summary>
    public static IServiceCollection AddDotnetServer(
        this IServiceCollection services,
        BuildServerEnvironment environment,
        DotnetDetection detection
    )
    {
        return services
            .AddBuildCore(environment)
            .AddSingleton(detection)
            .AddSingleton(static provider =>
                DotnetToolCatalog.Create(
                    detection: provider.GetRequiredService<DotnetDetection>(),
                    runner: provider.GetRequiredService<IJobRunner>()
                )
            )
            .AddSingleton(static provider =>
                ServerOptionsFactory.Create(
                    detection: provider.GetRequiredService<DotnetDetection>(),
                    tools: provider.GetRequiredService<IReadOnlyList<McpServerTool>>()
                )
            );
    }
}
