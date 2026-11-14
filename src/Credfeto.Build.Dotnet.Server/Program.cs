using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

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
                serve: ServeAsync,
                cancellationToken: CancellationToken.None
            )
            .AsTask();
    }

    private static async ValueTask<int> ServeAsync(CancellationToken cancellationToken)
    {
        // SIGTERM and Ctrl+C end the session cleanly, so a running job's process tree is killed rather than orphaned
        TaskCompletionSource signalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using PosixSignalRegistration terminate = PosixSignalRegistration.Create(
            signal: PosixSignal.SIGTERM,
            handler: context => Signal(context: context, signalled: signalled)
        );
        using PosixSignalRegistration interrupt = PosixSignalRegistration.Create(
            signal: PosixSignal.SIGINT,
            handler: context => Signal(context: context, signalled: signalled)
        );

        using CancellationTokenSource shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        BuildServerEnvironment environment = BuildServerEnvironment.FromProcess();
        DotnetDetection detection = await DotnetDetector.DetectAsync(
            repositoryRoot: environment.RepositoryRoot,
            cancellationToken: shutdown.Token
        );

        await using ServiceProvider services = new ServiceCollection()
            .AddDotnetServer(environment: environment, detection: detection)
            .BuildServiceProvider();

        await using StdioServerTransport transport = new(
            services.GetRequiredService<McpServerOptions>(),
            NullLoggerFactory.Instance
        );

        Task server = ServerRunner
            .RunAsync(services: services, transport: transport, cancellationToken: shutdown.Token)
            .AsTask();

        if (await Task.WhenAny(server, signalled.Task) != server)
        {
            await shutdown.CancelAsync();
        }

        await server.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        return 0;
    }

    private static void Signal(PosixSignalContext context, TaskCompletionSource signalled)
    {
        context.Cancel = true;
        signalled.TrySetResult();
    }
}
