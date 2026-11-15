using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server;

public static class StdioServerHost
{
    // Serves MCP over stdio until the client disconnects, the token is cancelled or the process gets SIGTERM or SIGINT
    public static async ValueTask<int> ServeAsync<TServiceProvider>(
        Func<CancellationToken, ValueTask<TServiceProvider>> createServices,
        CancellationToken cancellationToken
    )
        where TServiceProvider : IServiceProvider, IAsyncDisposable
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

        await using TServiceProvider services = await createServices(shutdown.Token);

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
