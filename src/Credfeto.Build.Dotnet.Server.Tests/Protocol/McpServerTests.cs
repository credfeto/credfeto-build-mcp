using System;
using System.IO;
using System.IO.Pipelines;
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
    [Fact]
    public async Task DotnetRepositoryAdvertisesItsToolsAsync()
    {
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());

        McpProbeResult result = await this.ProbeAsync(repository);

        Assert.Equal(
            expected: ["detect", "restore", "build", "test", "coverage", "job_status", "job_result"],
            actual: result.ToolNames,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: result.AdvertisesTools, userMessage: "the tools capability is advertised");
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
