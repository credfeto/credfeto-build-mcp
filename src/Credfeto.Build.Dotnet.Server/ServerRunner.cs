using System;
using System.Threading;
using System.Threading.Tasks;
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

        await server.RunAsync(cancellationToken);
    }
}
