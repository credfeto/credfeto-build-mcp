using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Dotnet.Server;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        return CommandLine
            .RunAsync(
                arguments: args,
                output: Console.Out,
                error: Console.Error,
                resolveEnvironment: BuildServerEnvironment.FromProcessAsync,
                serve: ServeAsync,
                cancellationToken: CancellationToken.None
            )
            .AsTask();
    }

    private static ValueTask<int> ServeAsync(BuildServerEnvironment environment, CancellationToken cancellationToken)
    {
        return StdioServerHost.ServeAsync(
            createServices: token => CreateServicesAsync(environment: environment, cancellationToken: token),
            cancellationToken: cancellationToken
        );
    }

    private static async ValueTask<ServiceProvider> CreateServicesAsync(
        BuildServerEnvironment environment,
        CancellationToken cancellationToken
    )
    {
        DotnetDetection detection = await DotnetDetector.DetectAsync(
            repositoryRoot: environment.RepositoryRoot,
            cancellationToken: cancellationToken
        );

        return new ServiceCollection()
            .AddDotnetServer(environment: environment, detection: detection)
            .BuildServiceProvider();
    }
}
