using System;
using System.Threading.Tasks;
using Credfeto.Build.Core.Overrides;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Overrides;

public sealed class OverridesSectionTests : TestBase
{
    [Fact]
    public async Task NodeSettingsLeaveTheDotnetSettingsInForceAsync()
    {
        OverridesLoadResult result = await this.LoadAsync(
            """
            {
              "dotnet": { "solution": "src/Example.slnx" },
              "node": {
                "enabled": true,
                "packages": ["web"],
                "testScript": "test:unit",
                "coverageScript": "test:coverage",
                "coverageFile": "coverage/cobertura-coverage.xml",
                "timeoutMinutes": 20,
                "memoryPerWorkerMb": 2048
              }
            }
            """
        );

        Assert.Null(result.DotnetErrors);
        Assert.Null(result.NodeErrors);
        Assert.Equal(expected: "src/Example.slnx", actual: result.Overrides.Dotnet?.Solution);
        NodeOverrides? node = result.Overrides.Node;
        Assert.NotNull(node);
        Assert.True(condition: node.Enabled, userMessage: "enabled was read");
        Assert.Equal(expected: ["web"], actual: node.Packages, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: "test:unit", actual: node.TestScript);
        Assert.Equal(expected: "test:coverage", actual: node.CoverageScript);
        Assert.Equal(expected: "coverage/cobertura-coverage.xml", actual: node.CoverageFile);
        Assert.Equal(expected: 20, actual: node.TimeoutMinutes);
        Assert.Equal(expected: 2048, actual: node.MemoryPerWorkerMb);
    }

    [Fact]
    public async Task ANodeSectionThatIsNotValidOnlyCostsNodeItsSettingsAsync()
    {
        OverridesLoadResult result = await this.LoadAsync(
            """
            {
              "dotnet": { "solution": "src/Example.slnx" },
              "node": { "secretCommand": "curl evil" }
            }
            """
        );

        Assert.Null(result.DotnetErrors);
        Assert.Equal(expected: "src/Example.slnx", actual: result.Overrides.Dotnet?.Solution);
        Assert.Null(result.Overrides.Node);
        Assert.Equal(
            expected: ".ai/commands.json: the node section is not valid (line 3) and was ignored",
            actual: result.NodeErrors
        );
    }

    [Fact]
    public async Task ADotnetSectionThatIsNotValidOnlyCostsDotnetItsSettingsAsync()
    {
        OverridesLoadResult result = await this.LoadAsync(
            """
            {
              "dotnet": { "command": "rm -rf /" },
              "node": { "enabled": false }
            }
            """
        );

        Assert.Null(result.Overrides.Dotnet);
        Assert.NotNull(result.DotnetErrors);
        Assert.DoesNotContain(
            expectedSubstring: "rm -rf",
            actualString: result.DotnetErrors,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Null(result.NodeErrors);
        Assert.False(condition: result.Overrides.Node?.Enabled, userMessage: "the node section was still read");
    }

    [Fact]
    public async Task AnUnknownSettingIsReportedButTheKnownSectionsStayAsync()
    {
        OverridesLoadResult result = await this.LoadAsync(
            """
            {
              "dotnet": { "timeoutMinutes": 45 },
              "ruby": { "anything": "here" }
            }
            """
        );

        Assert.Equal(expected: 45, actual: result.Overrides.Dotnet?.TimeoutMinutes);
        Assert.Equal(
            expected: ".ai/commands.json has a setting that is not recognised, which was ignored",
            actual: result.Error
        );
        Assert.Equal(expected: result.Error, actual: result.DotnetErrors);
        Assert.Equal(expected: result.Error, actual: result.NodeErrors);
        Assert.DoesNotContain(
            expectedSubstring: "ruby",
            actualString: result.Error,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task PythonSettingsAreReadAlongsideTheOtherSectionsAsync()
    {
        OverridesLoadResult result = await this.LoadAsync(
            """
            {
              "dotnet": { "timeoutMinutes": 45 },
              "python": { "enabled": true, "projects": ["tools/scripts"], "timeoutMinutes": 15 },
            }
            """
        );

        Assert.Null(result.DotnetErrors);
        Assert.Null(result.PythonErrors);
        Assert.Equal(expected: 45, actual: result.Overrides.Dotnet?.TimeoutMinutes);
        PythonOverrides? python = result.Overrides.Python;
        Assert.NotNull(python);
        Assert.True(condition: python.Enabled, userMessage: "enabled was read");
        Assert.Equal(expected: ["tools/scripts"], actual: python.Projects, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: 15, actual: python.TimeoutMinutes);
    }

    [Fact]
    public async Task APythonSectionThatIsNotValidOnlyCostsPythonItsSettingsAsync()
    {
        OverridesLoadResult result = await this.LoadAsync(
            """
            {
              "dotnet": { "timeoutMinutes": 45 },
              "node": { "enabled": false },
              "python": { "anything": ["the", "python", "server", "reads"] },
            }
            """
        );

        Assert.Null(result.DotnetErrors);
        Assert.Null(result.NodeErrors);
        Assert.Equal(expected: 45, actual: result.Overrides.Dotnet?.TimeoutMinutes);
        Assert.False(condition: result.Overrides.Node?.Enabled, userMessage: "the node section was still read");
        Assert.Null(result.Overrides.Python);
        Assert.Equal(
            expected: ".ai/commands.json: the python section is not valid (line 4) and was ignored",
            actual: result.PythonErrors
        );
    }

    [Fact]
    public async Task ANullSectionMeansNoSettingsAndNoErrorAsync()
    {
        OverridesLoadResult result = await this.LoadAsync("""{ "dotnet": null, "node": null, "python": null }""");

        Assert.Same(expected: RepositoryOverrides.None, actual: result.Overrides);
        Assert.Null(result.DotnetErrors);
        Assert.Null(result.NodeErrors);
        Assert.Null(result.PythonErrors);
    }

    [Theory]
    [InlineData("""{ "node": { "enabled": true } """)]
    [InlineData("""{ "node": { "enabled": true } } {}""")]
    [InlineData("""[]""")]
    [InlineData("")]
    public async Task AFileThatIsNotOneJsonObjectIsIgnoredWholeAsync(string content)
    {
        OverridesLoadResult result = await this.LoadAsync(content);

        Assert.Same(expected: RepositoryOverrides.None, actual: result.Overrides);
        Assert.NotNull(result.Error);
        Assert.Null(result.NodeError);
        Assert.Null(result.DotnetError);
        Assert.Null(result.PythonError);
    }

    private async ValueTask<OverridesLoadResult> LoadAsync(string content)
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(
            relative: OverridesLoader.RELATIVE_PATH,
            content: content,
            cancellationToken: this.CancellationToken()
        );

        return await OverridesLoader.LoadAsync(
            repositoryRoot: repository.Path,
            cancellationToken: this.CancellationToken()
        );
    }
}
