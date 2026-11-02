using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerRunner
{
    /// <summary>Serves MCP over the transport until the client disconnects or the token is cancelled.</summary>
    public static async ValueTask RunAsync(IServiceProvider services, ITransport transport, CancellationToken cancellationToken)
    {
        McpServerOptions options = services.GetRequiredService<McpServerOptions>();

        await using McpServer server = McpServer.Create(
            transport: transport,
            serverOptions: options,
            loggerFactory: NullLoggerFactory.Instance,
            serviceProvider: services
        );

        // Watching starts once the server is up, off the request path, so the first tools/list is not held up
        RepositoryWatcher watcher = services.GetRequiredService<RepositoryWatcher>();
        Task<bool> watching = Task.Run(function: watcher.Start, cancellationToken: CancellationToken.None);

        await server.RunAsync(cancellationToken);
        _ = await watching;
    }
}
