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

namespace Credfeto.Build.Server.Tests.Startup;

// Drives job_result over the real stdio transport, as an agent would, against a real runner whose steps the test holds
public sealed class JobResultWaitTests : TestBase, IDisposable
{
    private const string PROGRESS_TOKEN = "wait-for-build";

    private readonly BlockingProcessLauncher _launcher = new();
    private readonly TemporaryDirectory _logs = new();
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._logs.Dispose();
        this._repository.Dispose();
    }

    [Fact]
    public async Task JobResultWaitsForTheJobWhileJobStatusStillAnswersAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await using JobRunner runner = new(
            launcher: this._launcher,
            masker: new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal)),
            settings: JobRunnerSettings.Create(this._logs.Path),
            timeProvider: TimeProvider.System
        );
        JobStatus started = await runner.StartAsync(
            new(
                Kind: "build",
                Steps: [Step("restore"), Step("build")],
                Timeout: TimeSpan.FromMinutes(5),
                ContinueOnFailure: false,
                TestParser: null,
                CoverageFiles: [],
                HeadSha: null
            )
        );

        McpServerPrimitiveCollection<McpServerTool> tools = [new JobStatusTool(runner), new JobResultTool(runner)];

        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton(
                McpServerOptionsFactory.Create(
                    name: "example-server",
                    versionSource: typeof(JobResultWaitTests).Assembly,
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

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        Task client = this.ClientAsync(
            clientToServer: clientToServer,
            serverToClient: serverToClient,
            jobId: started.Id,
            cancellationToken: cancellationToken
        );

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);
        await client;
    }

    private async Task ClientAsync(
        Pipe clientToServer,
        Pipe serverToClient,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        await using Stream toServer = clientToServer.Writer.AsStream();
        using StreamReader fromServer = new(serverToClient.Reader.AsStream());

        try
        {
            McpSession session = new(toServer: toServer, fromServer: fromServer);
            await session.InitializeAsync(cancellationToken);

            Assert.Equal(expected: "restore", actual: await this._launcher.WaitForStepAsync(cancellationToken));

            int waiting = await session.SendToolCallAsync(
                name: JobToolNames.JOB_RESULT,
                argumentsJson: "{\"id\":\"" + jobId + "\",\"wait_seconds\":600}",
                progressToken: PROGRESS_TOKEN,
                cancellationToken: cancellationToken
            );
            int looking = await session.SendToolCallAsync(
                name: JobToolNames.JOB_STATUS,
                argumentsJson: "{\"id\":\"" + jobId + "\"}",
                progressToken: null,
                cancellationToken: cancellationToken
            );

            // job_result is still waiting on the held step, so this answer can only come if the calls run side by side
            Assert.Contains(
                expectedSubstring: "\"state\":\"running\"",
                actualString: await session.ReceiveToolAnswerAsync(id: looking, cancellationToken: cancellationToken),
                comparisonType: StringComparison.Ordinal
            );

            this._launcher.Release();
            Assert.Equal(expected: "build", actual: await this._launcher.WaitForStepAsync(cancellationToken));
            this._launcher.Release();

            Assert.Contains(
                expectedSubstring: "\"state\":\"succeeded\"",
                actualString: await session.ReceiveToolAnswerAsync(id: waiting, cancellationToken: cancellationToken),
                comparisonType: StringComparison.Ordinal
            );
            Assert.True(
                condition: session.ProgressNotifications >= 1,
                userMessage: "a call that carried a progress token is told the job is still going"
            );
        }
        finally
        {
            // End of input ends the session, as when a client closes the server's stdin
            await clientToServer.Writer.CompleteAsync();
        }
    }

    private static ProcessStep Step(string name)
    {
        return new(
            Name: name,
            FileName: "dotnet",
            Arguments: [name],
            WorkingDirectory: "/",
            Environment: new Dictionary<string, string>(StringComparer.Ordinal)
        );
    }
}
