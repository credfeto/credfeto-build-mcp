using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Tools;

public static class ToolProgress
{
    public static IToolProgress None { get; } = new NoProgress();

    public static IToolProgress For(RequestContext<CallToolRequestParams> request, TextWriter error)
    {
        return request.Params?.ProgressToken is { } token
            ? new ClientProgress(server: request.Server, token: token, error: error)
            : None;
    }

    private sealed class NoProgress : IToolProgress
    {
        public bool Enabled => false;

        public ValueTask ReportAsync(double progress, string message, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ClientProgress : IToolProgress
    {
        private readonly TextWriter _error;
        private readonly McpServer _server;
        private readonly ProgressToken _token;

        public ClientProgress(McpServer server, ProgressToken token, TextWriter error)
        {
            this._server = server;
            this._token = token;
            this._error = error;
        }

        public bool Enabled => true;

        public async ValueTask ReportAsync(double progress, string message, CancellationToken cancellationToken)
        {
            try
            {
                await this._server.NotifyProgressAsync(
                    progressToken: this._token,
                    progress: new() { Progress = (float)progress, Message = message },
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // A notification that cannot be sent leaves the tool's work to carry on; stderr only, stdout is the
                // MCP stream
                await this._error.WriteLineAsync(
                    buffer: "A progress notification could not be sent".AsMemory(),
                    cancellationToken: cancellationToken
                );
            }
        }
    }
}
