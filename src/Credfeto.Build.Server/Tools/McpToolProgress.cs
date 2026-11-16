using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Tools;

// Sends notifications/progress for the call that carried the progress token
public sealed class McpToolProgress : IToolProgress
{
    private readonly ProgressToken _progressToken;
    private readonly McpServer _server;

    public McpToolProgress(McpServer server, ProgressToken progressToken)
    {
        this._server = server;
        this._progressToken = progressToken;
    }

    public async ValueTask ReportAsync(int progress, int total, string message, CancellationToken cancellationToken)
    {
        try
        {
            await this._server.NotifyProgressAsync(
                progressToken: this._progressToken,
                progress: new()
                {
                    Progress = progress,
                    Total = total,
                    Message = message,
                },
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (exception is McpException or IOException or ObjectDisposedException)
        {
            // Progress is only a courtesy: a notification that cannot be sent never fails the call it reports on
            await Console.Error.WriteLineAsync(
                buffer: ("Progress notification not sent: " + exception.Message).AsMemory(),
                cancellationToken: cancellationToken
            );
        }
    }
}
