using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Server;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Protocol;

public sealed class McpServerTests : TestBase
{
    private static readonly string[] AllTools = ["detect", "job_result", "job_status", "lint", "restore", "test"];

    [Fact]
    public async Task APythonProjectAdvertisesItsToolsAsync()
    {
        using TemporaryDirectory repository = new();
        await PythonRepositories.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());

        McpProbeResult result = await this.ProbeAsync(repository);

        Assert.Equal(expected: AllTools, actual: result.ToolNames, comparer: StringComparer.Ordinal);
        Assert.True(condition: result.AdvertisesTools, userMessage: "the tools capability is advertised");
        Assert.True(condition: result.AdvertisesListChanged, userMessage: "tools.listChanged is advertised");
        Assert.Contains(
            expectedSubstring: "the Python projects in",
            actualString: result.Instructions,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ARepositoryWithoutAManifestAdvertisesNoToolsAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(
            relative: "pyproject.toml",
            content: "[tool.black]\nline-length = 120\n",
            cancellationToken: this.CancellationToken()
        );

        McpProbeResult result = await this.ProbeAsync(repository);

        Assert.Empty(result.ToolNames);
        Assert.Contains(
            expectedSubstring: "offers no tools",
            actualString: result.Instructions,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ATestSuiteWithoutAManifestAdvertisesDetectAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(
            relative: "scripts/tests/test_zone.py",
            content: "import unittest\n",
            cancellationToken: this.CancellationToken()
        );

        McpProbeResult result = await this.ProbeAsync(repository);

        Assert.Equal(
            expected: ["detect", "job_result", "job_status", "restore"],
            actual: result.ToolNames,
            comparer: StringComparer.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "There are Python test files in",
            actualString: result.Instructions,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task TheToolListFollowsTheRepositoryWithListChangedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory repository = new();
        BuildServerEnvironment environment = Environment(repository);
        PythonDetection detection = await PythonDetector.DetectAsync(
            repositoryRoot: repository.Path,
            cancellationToken: cancellationToken
        );

        await using ServiceProvider services = new ServiceCollection()
            .AddPythonServer(
                environment: environment,
                detection: detection,
                python: TestTools.NoPython,
                debounce: TimeSpan.FromMilliseconds(100)
            )
            .BuildServiceProvider();

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        string manifest = repository.Combine("pyproject.toml");
        Task client = WatchingClientAsync(
            addProject: token => PythonRepositories.WriteAsync(repository: repository, cancellationToken: token),
            manifest: manifest,
            clientToServer: clientToServer,
            serverToClient: serverToClient,
            cancellationToken: cancellationToken
        );

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);
        await client;
    }

    private static async Task WatchingClientAsync(
        Func<CancellationToken, ValueTask> addProject,
        string manifest,
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

            await addProject(timeout.Token);
            await WaitForToolsAsync(session: session, expected: AllTools, cancellationToken: timeout.Token);

            File.Delete(manifest);
            await WaitForToolsAsync(session: session, expected: [], cancellationToken: timeout.Token);
        }
        finally
        {
            await clientToServer.Writer.CompleteAsync();
        }
    }

    private static async ValueTask WaitForToolsAsync(
        McpSession session,
        IReadOnlyList<string> expected,
        CancellationToken cancellationToken
    )
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

    private async ValueTask<McpProbeResult> ProbeAsync(TemporaryDirectory repository)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        PythonDetection detection = await PythonDetector.DetectAsync(
            repositoryRoot: repository.Path,
            cancellationToken: cancellationToken
        );

        await using ServiceProvider services = new ServiceCollection()
            .AddPythonServer(environment: Environment(repository), detection: detection)
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
            cancellationToken: cancellationToken
        );

        await ServerRunner.RunAsync(services: services, transport: transport, cancellationToken: cancellationToken);

        return await client;
    }

    private static BuildServerEnvironment Environment(TemporaryDirectory repository)
    {
        return new(
            RepositoryRoot: repository.Path,
            RepositoryRootSource: RepositoryRootSource.Variable,
            LogDirectory: repository.Combine("logs")
        );
    }

    private static async Task<McpProbeResult> ClientAsync(
        Pipe clientToServer,
        Pipe serverToClient,
        CancellationToken cancellationToken
    )
    {
        await using Stream toServer = clientToServer.Writer.AsStream();
        using StreamReader fromServer = new(serverToClient.Reader.AsStream());

        try
        {
            return await McpProbe.ListToolsAsync(
                toServer: toServer,
                fromServer: fromServer,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            // End of input ends the session, as when a client closes the server's stdin
            await clientToServer.Writer.CompleteAsync();
        }
    }
}
