using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Workspace;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Server.Tests.Startup;

public sealed class ServerRunnerTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task TheServerListsTheLiveToolsAndWatchesTheRepositoryAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        RecordingChangeHandler handler = new();
        McpServerPrimitiveCollection<McpServerTool> tools = [new FakeTool("detect"), new FakeTool("build")];

        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton(
                McpServerOptionsFactory.Create(
                    name: "example-server",
                    versionSource: typeof(ServerRunnerTests).Assembly,
                    instructions: "Does things",
                    tools: tools
                )
            )
            .AddSingleton(_ => new RepositoryWatcher(
                repositoryRoot: this._repository.Path,
                source: handler,
                filter: new(Extensions: [".proj"], IgnoredDirectories: []),
                timeProvider: TimeProvider.System,
                debounce: TimeSpan.FromMilliseconds(50)
            ))
            .BuildServiceProvider();

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        Task<McpProbeResult> client = ClientAsync(
            clientToServer: clientToServer,
            serverToClient: serverToClient,
            waitForWatch: () => handler.Calls != 0,
            cancellationToken: cancellationToken
        );

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);
        McpProbeResult result = await client;

        Assert.Equal(expected: ["detect", "build"], actual: result.ToolNames, comparer: StringComparer.Ordinal);
        Assert.True(condition: result.AdvertisesListChanged, userMessage: "list changes are advertised");
        Assert.Equal(expected: "Does things", actual: result.Instructions);
        Assert.True(condition: services.GetRequiredService<RepositoryWatcher>().IsWatching, userMessage: "watched");
    }

    private static async Task<McpProbeResult> ClientAsync(
        Pipe clientToServer,
        Pipe serverToClient,
        Func<bool> waitForWatch,
        CancellationToken cancellationToken
    )
    {
        await using Stream toServer = clientToServer.Writer.AsStream();
        using StreamReader fromServer = new(serverToClient.Reader.AsStream());

        try
        {
            McpProbeResult result = await McpProbe.ListToolsAsync(
                toServer: toServer,
                fromServer: fromServer,
                cancellationToken: cancellationToken
            );

            // The watch starts its first look once the server is up
            await Waiting.ForAsync(
                condition: waitForWatch,
                what: "the watch to start",
                cancellationToken: cancellationToken
            );

            return result;
        }
        finally
        {
            // End of input ends the session, as when a client closes the server's stdin
            await clientToServer.Writer.CompleteAsync();
        }
    }
}
