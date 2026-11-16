using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tools;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Server.Tests.Tools;

public sealed class ToolProgressTests : TestBase
{
    [Fact]
    public async Task AProgressNotificationThatCannotBeSentDoesNotFailTheCallAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        Pipe input = new();

        // Output that has gone, as when the client goes away while a tool is still working
        MemoryStream output = new();
        await output.DisposeAsync();

        await using StreamServerTransport transport = new(
            input.Reader.AsStream(),
            output,
            "test",
            NullLoggerFactory.Instance
        );
        await using McpServer server = McpServer.Create(
            transport: transport,
            serverOptions: new(),
            loggerFactory: NullLoggerFactory.Instance
        );

        McpToolProgress progress = new(server: server, progressToken: new("wait-for-build"));

        Exception? thrown = await Record.ExceptionAsync(async () =>
            await progress.ReportAsync(
                progress: 0,
                total: 2,
                message: "0/2 running build",
                cancellationToken: cancellationToken
            )
        );

        Assert.Null(thrown);

        await input.Writer.CompleteAsync();
    }
}
