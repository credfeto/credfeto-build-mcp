using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Protocol;

// Starts the published Native AOT binary named by BUILD_MCP_SERVER_BINARY and times process start to the tools/list
// reply, detection included. Skipped when the variable is not set, so a plain dotnet test run needs no publish step
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
        await NpmRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());

        List<TimeSpan> timings = [];

        for (int run = 0; run < RUNS; run++)
        {
            TimedProbeResult probe = await this.StartAndListAsync(binary: binary, repository: repository);
            Assert.Contains(expected: "detect", collection: probe.Result.ToolNames, comparer: StringComparer.Ordinal);
            timings.Add(probe.Elapsed);
        }

        TimeSpan median = timings.Order().ElementAt(RUNS / 2);
        string report =
            "start to tools/list: "
            + string.Join(separator: ", ", values: timings.Select(Milliseconds))
            + "; median "
            + Milliseconds(median);
        TestContext.Current.TestOutputHelper?.WriteLine(report);

        Assert.True(condition: median < Limit, userMessage: report);
    }

    [Fact]
    public async Task PublishedServerAdvertisesNoToolsWithoutPackageJsonAsync()
    {
        string binary = RequireBinary();
        using TemporaryDirectory repository = new();

        TimedProbeResult probe = await this.StartAndListAsync(binary: binary, repository: repository);

        Assert.Empty(probe.Result.ToolNames);
    }

    private static string RequireBinary()
    {
        string? binary = Environment.GetEnvironmentVariable(BINARY_VARIABLE);
        Assert.SkipWhen(
            condition: string.IsNullOrEmpty(binary),
            reason: BINARY_VARIABLE + " is not set to a published server binary"
        );

        return binary ?? string.Empty;
    }

    private ValueTask<TimedProbeResult> StartAndListAsync(string binary, TemporaryDirectory repository)
    {
        return McpServerProcess.StartAndListAsync(
            binary: binary,
            repositoryRoot: repository.Path,
            logDirectory: repository.Combine("logs"),
            cancellationToken: this.CancellationToken()
        );
    }

    private static string Milliseconds(TimeSpan value)
    {
        return value.TotalMilliseconds.ToString(format: "0.0", provider: CultureInfo.InvariantCulture) + " ms";
    }
}
