using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Python.Server.Tools;
using Credfeto.Build.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Tools;

public sealed class DetectToolTests : TestBase
{
    private const string ROOT = "/work/repo";

    [Fact]
    public void NoProjectOffersNoTools()
    {
        IReadOnlyList<McpServerTool> tools = TestTools.Catalog(TestTools.NotDetected(ROOT));

        Assert.Empty(tools);
    }

    [Fact]
    public void AProjectOffersTheJobTools()
    {
        IReadOnlyList<McpServerTool> tools = TestTools.Catalog(TestTools.Detected(Repository()));

        Assert.Equal(
            expected: ["detect", "restore", "test", "lint", "coverage", "job_status", "job_result"],
            actual: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task DetectReportsTheProjectsAsync()
    {
        BuildTool detect = TestTools.Find(
            tools: TestTools.Catalog(TestTools.Detected(Repository())),
            name: ToolNames.DETECT
        );

        CallToolResult result = await detect.CallAsync(arguments: null, cancellationToken: this.CancellationToken());

        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        using JsonDocument document = JsonDocument.Parse(TestTools.Text(result));
        JsonElement root = document.RootElement;
        Assert.Equal(expected: TestTools.PYTHON, actual: root.GetProperty("interpreter").GetString());
        Assert.Equal(expected: 30, actual: root.GetProperty("timeoutMinutes").GetInt32());
        JsonElement project = root.GetProperty("projects")[0];
        Assert.Equal(expected: "example", actual: project.GetProperty("name").GetString());
        Assert.Equal(expected: ".", actual: project.GetProperty("directory").GetString());
        Assert.Equal(expected: "pip", actual: project.GetProperty("installer").GetString());
        Assert.Equal(expected: ">=3.11", actual: project.GetProperty("requiresPython").GetString());
        Assert.Equal(expected: "dev", actual: project.GetProperty("testExtras")[0].GetString());
        Assert.True(condition: project.GetProperty("declaresPytest").GetBoolean(), userMessage: "pytest is declared");
        Assert.False(condition: project.GetProperty("declaresCoverage").GetBoolean(), userMessage: "coverage is not");
        JsonElement unsupported = root.GetProperty("projects")[1];
        Assert.Equal(expected: "unsupported", actual: unsupported.GetProperty("installer").GetString());
        Assert.Equal(
            expected: "src/poetry.lock means poetry, which is not supported: only pip and uv are",
            actual: unsupported.GetProperty("unsupported").GetString()
        );
        Assert.False(
            condition: root.TryGetProperty(propertyName: "testSuiteWithoutManifest", value: out _),
            userMessage: "there are manifests"
        );
    }

    [Fact]
    public async Task ATestSuiteWithoutAManifestGetsDetectWithItsDirectoriesAsync()
    {
        PythonDetection detection = TestTools.NotDetected(ROOT) with { TestSuite = new(["scripts/tests"]) };
        IReadOnlyList<McpServerTool> tools = TestTools.Catalog(detection);
        BuildTool detect = TestTools.Find(tools: tools, name: ToolNames.DETECT);

        CallToolResult result = await detect.CallAsync(arguments: null, cancellationToken: this.CancellationToken());

        Assert.Equal(
            expected: ["detect", "restore", "job_status", "job_result"],
            actual: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.False(condition: result.IsError, userMessage: TestTools.Text(result));
        using JsonDocument document = JsonDocument.Parse(TestTools.Text(result));
        JsonElement testSuite = document.RootElement.GetProperty("testSuiteWithoutManifest");
        Assert.Equal(expected: "scripts/tests", actual: testSuite.GetProperty("directories")[0].GetString());
        Assert.Equal(expected: DetectTool.TEST_SUITE_NOTE, actual: testSuite.GetProperty("note").GetString());
        Assert.Equal(expected: 0, actual: document.RootElement.GetProperty("projects").GetArrayLength());
    }

    [Fact]
    public async Task DetectFailsOnceTheProjectsHaveGoneAsync()
    {
        DetectTool detect = new(TestTools.Context(TestTools.NotDetected(ROOT)));

        CallToolResult result = await detect.CallAsync(arguments: null, cancellationToken: this.CancellationToken());

        Assert.True(condition: result.IsError, userMessage: "detect reports the projects have gone");
        Assert.Contains(
            expectedSubstring: "There is no Python project any more",
            actualString: TestTools.Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    private static PythonRepository Repository()
    {
        return new(
            RepositoryRoot: ROOT,
            Projects:
            [
                new PythonProject(Name: "example", RelativeDirectory: ".", Installer: PythonInstaller.Pip)
                {
                    Manifests = ["pyproject.toml"],
                    RequiresPython = ">=3.11",
                    TestExtras = ["dev"],
                    DeclaresPytest = true,
                },
                new PythonProject(Name: "src", RelativeDirectory: "src", Installer: PythonInstaller.Unsupported)
                {
                    Manifests = ["pyproject.toml", "poetry.lock"],
                    Problem = "src/poetry.lock means poetry, which is not supported: only pip and uv are",
                },
            ],
            Timeout: TimeSpan.FromMinutes(30)
        );
    }
}
