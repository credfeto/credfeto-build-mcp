using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Protocol;

/// <summary>The whole server in process over a pipe: what a client sees from tools/list.</summary>
public sealed class McpServerTests : TestBase
{
    private static readonly string[] AllTools =
    [
        "detect",
        "restore",
        "build",
        "test",
        "coverage",
        "buildcheck",
        "cleanup",
        "changelog_lint",
        "buildtest",
        "job_status",
        "job_result",
    ];

    [Fact]
    public async Task DotnetRepositoryAdvertisesItsToolsAsync()
    {
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());

        McpProbeResult result = await this.ProbeAsync(repository);

        Assert.Equal(expected: AllTools, actual: result.ToolNames, comparer: StringComparer.Ordinal);
        Assert.True(condition: result.AdvertisesTools, userMessage: "the tools capability is advertised");
        Assert.True(condition: result.AdvertisesListChanged, userMessage: "tools.listChanged is advertised");
    }

    [Fact]
    public async Task TheToolListFollowsTheRepositoryWithListChangedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory repository = new();
        BuildServerEnvironment environment = new(RepositoryRoot: repository.Path, LogDirectory: repository.Combine("logs"));
        DotnetDetection detection = await DotnetDetector.DetectAsync(repositoryRoot: repository.Path, cancellationToken: cancellationToken);

        await using ServiceProvider services = new ServiceCollection()
            .AddDotnetServer(environment: environment, detection: detection, buildtest: TestContexts.NoBuildtest, debounce: TimeSpan.FromMilliseconds(100))
            .BuildServiceProvider();

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        string solution = repository.Combine("src/Example.slnx");
        Task client = WatchingClientAsync(
            addDotnet: token => SampleRepository.WriteAsync(repository: repository, cancellationToken: token),
            solution: solution,
            clientToServer: clientToServer,
            serverToClient: serverToClient,
            cancellationToken: cancellationToken
        );

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);
        await client;
    }

    private static async Task WatchingClientAsync(
        Func<CancellationToken, ValueTask> addDotnet,
        string solution,
        Pipe clientToServer,
        Pipe serverToClient,
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
            Assert.Empty(await session.ListToolsAsync(timeout.Token));

            await addDotnet(timeout.Token);
            await WaitForToolsAsync(session: session, expected: AllTools, cancellationToken: timeout.Token);

            File.Delete(solution);
            await WaitForToolsAsync(session: session, expected: [], cancellationToken: timeout.Token);
        }
        finally
        {
            await clientToServer.Writer.CompleteAsync();
        }
    }

    private static async ValueTask WaitForToolsAsync(McpSession session, IReadOnlyList<string> expected, CancellationToken cancellationToken)
    {
        // The change may land in more than one refresh; each one is announced, so wait until the list matches
        while (true)
        {
            await session.WaitForListChangedAsync(cancellationToken);
            IReadOnlyList<string> tools = await session.ListToolsAsync(cancellationToken);

            if (tools.SequenceEqual(second: expected, comparer: StringComparer.Ordinal))
            {
                return;
            }
        }
    }

    [Fact]
    public async Task RepositoryWithoutDotnetAdvertisesNoToolsAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(relative: "package.json", content: "{}", cancellationToken: this.CancellationToken());

        McpProbeResult result = await this.ProbeAsync(repository);

        Assert.Empty(result.ToolNames);
        Assert.Contains(expectedSubstring: "no tools", actualString: result.Instructions, comparisonType: StringComparison.Ordinal);
    }

    private async ValueTask<McpProbeResult> ProbeAsync(TemporaryDirectory repository)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        BuildServerEnvironment environment = new(RepositoryRoot: repository.Path, LogDirectory: repository.Combine("logs"));
        DotnetDetection detection = await DotnetDetector.DetectAsync(repositoryRoot: repository.Path, cancellationToken: cancellationToken);

        await using ServiceProvider services = new ServiceCollection()
            .AddDotnetServer(environment: environment, detection: detection)
            .BuildServiceProvider();

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        Task<McpProbeResult> client = ClientAsync(clientToServer: clientToServer, serverToClient: serverToClient, cancellationToken: cancellationToken);

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);

        return await client;
    }

    private static async Task<McpProbeResult> ClientAsync(Pipe clientToServer, Pipe serverToClient, CancellationToken cancellationToken)
    {
        await using Stream toServer = clientToServer.Writer.AsStream();
        using StreamReader fromServer = new(serverToClient.Reader.AsStream());

        try
        {
            return await McpProbe.ListToolsAsync(toServer: toServer, fromServer: fromServer, cancellationToken: cancellationToken);
        }
        finally
        {
            // End of input ends the session, as when a client closes the server's stdin
            await clientToServer.Writer.CompleteAsync();
        }
    }
}
