using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Tools;

public sealed class DotnetToolCatalogTests : TestBase
{
    [Fact]
    public static void NoDotnetMeansNoTools()
    {
        IReadOnlyList<McpServerTool> tools = TestContexts.Tools(
            runner: new RecordingJobRunner(),
            detection: TestRepositories.NotDetected("/workspace/repo")
        );

        Assert.Empty(tools);
    }

    [Fact]
    public static void DotnetRepositoryGetsEveryTool()
    {
        Assert.Equal(
            expected:
            [
                "build",
                "buildcheck",
                "buildtest",
                "changelog_lint",
                "cleanup",
                "coverage",
                "detect",
                "job_result",
                "job_status",
                "restore",
                "test",
            ],
            actual: Names(Create(TestRepositories.CoreTests, TestRepositories.ApiTests)),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void WithoutUnitTestProjectsThereIsNoTestOrCoverage()
    {
        Assert.Equal(
            expected:
            [
                "build",
                "buildcheck",
                "buildtest",
                "changelog_lint",
                "cleanup",
                "detect",
                "job_result",
                "job_status",
                "restore",
            ],
            actual: Names(Create()),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void TestTakesAScopeAndAnEnumOfUnitIntegrationAndBenchmarkTestProjects()
    {
        JsonElement schema = Schema(tools: Create(TestRepositories.CoreTests, TestRepositories.ApiTests), name: "test");

        Assert.Equal(
            expected: ["all", "project"],
            actual: Enum(schema: schema, property: "scope"),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected:
            [
                "Example.Core.Tests",
                "Example.Api.Tests",
                "Example.Integration.Tests",
                "Example.Benchmark.Tests",
            ],
            actual: Enum(schema: schema, property: "project"),
            comparer: StringComparer.Ordinal
        );
        Assert.False(
            condition: schema.GetProperty("additionalProperties").GetBoolean(),
            userMessage: "no other parameters"
        );
    }

    [Fact]
    public static void CoverageTakesAllOrAUnitTestProject()
    {
        JsonElement schema = Schema(tools: Create(TestRepositories.CoreTests), name: "coverage");

        Assert.Equal(
            expected: ["all", "Example.Core.Tests"],
            actual: Enum(schema: schema, property: "project"),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void EveryParameterIsAnEnumOrAPatternCheckedId()
    {
        foreach (McpServerTool tool in Create(TestRepositories.CoreTests))
        {
            foreach (JsonProperty property in tool.ProtocolTool.InputSchema.GetProperty("properties").EnumerateObject())
            {
                bool constrained =
                    property.Value.TryGetProperty(propertyName: "enum", value: out _)
                    || property.Value.TryGetProperty(propertyName: "pattern", value: out _);

                Assert.True(
                    condition: constrained,
                    userMessage: tool.ProtocolTool.Name + "." + property.Name + " is free-form"
                );
            }
        }
    }

    [Fact]
    public static void QualityToolsTakeOnlyEnums()
    {
        IReadOnlyList<McpServerTool> tools = Create(TestRepositories.CoreTests);

        Assert.Equal(
            expected: ["pre_release", "release"],
            actual: Enum(schema: Schema(tools: tools, name: "buildcheck"), property: "mode"),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: ["changed", "all"],
            actual: Enum(schema: Schema(tools: tools, name: "cleanup"), property: "scope"),
            comparer: StringComparer.Ordinal
        );
        Assert.Empty(Schema(tools: tools, name: "changelog_lint").GetProperty("properties").EnumerateObject());
        Assert.Empty(Schema(tools: tools, name: "buildtest").GetProperty("properties").EnumerateObject());
    }

    [Theory]
    [InlineData("detect", true)]
    [InlineData("job_status", true)]
    [InlineData("job_result", true)]
    [InlineData("build", false)]
    [InlineData("coverage", false)]
    [InlineData("cleanup", false)]
    [InlineData("buildtest", false)]
    public static void OnlyQueryToolsAreReadOnly(string name, bool readOnly)
    {
        McpServerTool tool = Create(TestRepositories.CoreTests)
            .Single(candidate => StringComparer.Ordinal.Equals(x: candidate.ProtocolTool.Name, y: name));

        Assert.Equal(expected: readOnly, actual: tool.ProtocolTool.Annotations?.ReadOnlyHint);
    }

    private static IReadOnlyList<McpServerTool> Create(
        params Credfeto.Build.Dotnet.Server.Detection.DotnetProject[] unitTests
    )
    {
        return TestContexts.Tools(
            runner: new RecordingJobRunner(),
            detection: TestRepositories.Detected(TestRepositories.Create(root: "/workspace/repo", unitTests))
        );
    }

    private static IEnumerable<string> Names(IReadOnlyList<McpServerTool> tools)
    {
        return tools.Select(static tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal);
    }

    private static JsonElement Schema(IReadOnlyList<McpServerTool> tools, string name)
    {
        return tools
            .Single(tool => StringComparer.Ordinal.Equals(x: tool.ProtocolTool.Name, y: name))
            .ProtocolTool.InputSchema;
    }

    private static IEnumerable<string> Enum(JsonElement schema, string property)
    {
        return schema
            .GetProperty("properties")
            .GetProperty(property)
            .GetProperty("enum")
            .EnumerateArray()
            .Select(static value => value.GetString() ?? string.Empty);
    }
}
