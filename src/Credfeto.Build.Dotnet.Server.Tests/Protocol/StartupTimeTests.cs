using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Protocol;

/// <summary>
///     Starts the published Native AOT binary named by BUILD_MCP_SERVER_BINARY and times process start to the
///     tools/list reply, detection included. Skipped when the variable is not set, so a plain dotnet test run
///     needs no publish step.
/// </summary>
public sealed class StartupTimeTests : TestBase
{
    public const string BINARY_VARIABLE = "BUILD_MCP_SERVER_BINARY";

    private const int RUNS = 5;

    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task PublishedServerListsItsToolsWithinTheLimitAsync()
    {
        string binary = RequireBinary();
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());

        List<TimeSpan> timings = [];

        for (int run = 0; run < RUNS; run++)
        {
            (McpProbeResult result, TimeSpan elapsed) = await this.StartAndListAsync(binary: binary, repository: repository);
            Assert.Contains(expected: "coverage", collection: result.ToolNames, comparer: StringComparer.Ordinal);
            timings.Add(elapsed);
        }

        TimeSpan median = timings.Order().ElementAt(RUNS / 2);
        string report = "start to tools/list: " + string.Join(separator: ", ", values: timings.Select(Milliseconds)) + "; median " + Milliseconds(median);
        TestContext.Current.TestOutputHelper?.WriteLine(report);

        Assert.True(condition: median < Limit, userMessage: report);
    }

    [Fact]
    public async Task PublishedServerAdvertisesNoToolsWithoutDotnetAsync()
    {
        string binary = RequireBinary();
        using TemporaryDirectory repository = new();

        (McpProbeResult result, _) = await this.StartAndListAsync(binary: binary, repository: repository);

        Assert.Empty(result.ToolNames);
    }

    private static string RequireBinary()
    {
        string? binary = Environment.GetEnvironmentVariable(BINARY_VARIABLE);
        Assert.SkipWhen(condition: string.IsNullOrEmpty(binary), reason: BINARY_VARIABLE + " is not set to a published server binary");

        return binary ?? string.Empty;
    }

    private async ValueTask<(McpProbeResult Result, TimeSpan Elapsed)> StartAndListAsync(string binary, TemporaryDirectory repository)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        ProcessStartInfo startInfo = new(binary)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment[BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE] = repository.Path;
        startInfo.Environment[BuildServerEnvironment.LOG_DIRECTORY_VARIABLE] = repository.Combine("logs");

        long started = Stopwatch.GetTimestamp();
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("The server did not start");

        McpProbeResult result = await McpProbe.ListToolsAsync(
            toServer: process.StandardInput.BaseStream,
            fromServer: process.StandardOutput,
            cancellationToken: cancellationToken
        );
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken);

        return (result, elapsed);
    }

    private static string Milliseconds(TimeSpan value)
    {
        return value.TotalMilliseconds.ToString(format: "0.0", provider: CultureInfo.InvariantCulture) + " ms";
    }
}
