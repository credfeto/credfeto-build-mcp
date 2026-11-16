using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Services;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Workspace;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Server.Tests.Tools;

public sealed class JobWaitProtocolTests : TestBase, IDisposable
{
    private const string PROGRESS_TOKEN = "wait-1";

    private readonly GatedProcessLauncher _launcher = new();
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task AnOpenWaitReportsProgressAndDoesNotHoldUpOtherRequestsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await using JobRunner runner = new(
            launcher: this._launcher,
            masker: new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal)),
            settings: JobRunnerSettings.Create(this._repository.Combine("logs")),
            timeProvider: TimeProvider.System
        );
        McpServerPrimitiveCollection<McpServerTool> tools = [new JobStatusTool(runner), new JobResultTool(runner)];

        await using ServiceProvider services = this.Services(tools);

        JobStatus started = await runner.StartAsync(GatedProcessLauncher.Job(TimeSpan.FromMinutes(5), "a"));
        await this._launcher.WaitForStartAsync(step: "a", cancellationToken: cancellationToken);

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        Task<IReadOnlyList<string>> client = this.ClientAsync(
            clientToServer: clientToServer,
            serverToClient: serverToClient,
            jobId: started.Id,
            cancellationToken: cancellationToken
        );

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);
        IReadOnlyList<string> progress = await client;

        Assert.NotEmpty(progress);
        Assert.All(collection: progress, action: static notification =>
            Assert.Contains(
                expectedSubstring: "\"progressToken\":\"" + PROGRESS_TOKEN + "\"",
                actualString: notification,
                comparisonType: StringComparison.Ordinal
            )
        );
    }

    private ServiceProvider Services(McpServerPrimitiveCollection<McpServerTool> tools)
    {
        return new ServiceCollection()
            .AddSingleton(
                McpServerOptionsFactory.Create(
                    name: "example-server",
                    versionSource: typeof(JobWaitProtocolTests).Assembly,
                    instructions: "Runs jobs",
                    tools: tools
                )
            )
            .AddSingleton(_ => new RepositoryWatcher(
                repositoryRoot: this._repository.Path,
                source: new RecordingChangeHandler(),
                filter: new(Extensions: [".proj"], IgnoredDirectories: []),
                timeProvider: TimeProvider.System,
                debounce: TimeSpan.FromMilliseconds(50)
            ))
            .BuildServiceProvider();
    }

    private async Task<IReadOnlyList<string>> ClientAsync(
        Pipe clientToServer,
        Pipe serverToClient,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        await using Stream toServer = clientToServer.Writer.AsStream();
        using StreamReader fromServer = new(serverToClient.Reader.AsStream());
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            McpSession session = new(toServer: toServer, fromServer: fromServer);
            await session.InitializeAsync(timeout.Token);
            string id = "{\"id\":\"" + jobId + "\"";

            int waiting = await session.CallToolAsync(
                name: "job_result",
                argumentsJson: id + ",\"wait_seconds\":120}",
                progressToken: PROGRESS_TOKEN,
                cancellationToken: timeout.Token
            );
            await session.WaitForProgressAsync(count: 1, cancellationToken: timeout.Token);

            int status = await session.CallToolAsync(
                name: "job_status",
                argumentsJson: id + "}",
                progressToken: null,
                cancellationToken: timeout.Token
            );
            AssertState(
                text: await session.ReceiveToolTextAsync(id: status, cancellationToken: timeout.Token),
                state: "running"
            );
            Assert.False(
                condition: session.HasResponse(waiting),
                userMessage: "job_status answered while the wait is open"
            );

            this._launcher.Release("a");
            AssertState(
                text: await session.ReceiveToolTextAsync(id: waiting, cancellationToken: timeout.Token),
                state: "succeeded"
            );

            return [.. session.ProgressReceived];
        }
        finally
        {
            // End of input ends the session, as when a client closes the server's stdin
            await clientToServer.Writer.CompleteAsync();
        }
    }

    private static void AssertState(string text, string state)
    {
        Assert.Contains(
            expectedSubstring: "\"state\":\"" + state + "\"",
            actualString: text,
            comparisonType: StringComparison.Ordinal
        );
    }
}
