using System.Threading.Tasks;
using Credfeto.Build.Core.Overrides;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Overrides;

public sealed class OverridesLoaderTests : TestBase
{
    [Fact]
    public async Task MissingFileGivesNoOverridesAndNoErrorAsync()
    {
        using TemporaryDirectory repository = new();

        OverridesLoadResult result = await OverridesLoader.LoadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken());

        Assert.Same(expected: RepositoryOverrides.None, actual: result.Overrides);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ValidFileIsReadWithCommentsAndTrailingCommasAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(
            cancellationToken: this.CancellationToken(),
            relative: OverridesLoader.RELATIVE_PATH,
            content: """
                {
                  // .NET settings
                  "dotnet": {
                    "enabled": true,
                    "solution": "src/Example.slnx",
                    "excludeTestProjects": ["Example.Slow.Tests"],
                    "timeoutMinutes": 45,
                  },
                }
                """
        );

        OverridesLoadResult result = await OverridesLoader.LoadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken());

        Assert.Null(result.Error);
        LanguageOverrides? dotnet = result.Overrides.Dotnet;
        Assert.NotNull(dotnet);
        Assert.True(condition: dotnet.Enabled, userMessage: "enabled was read");
        Assert.Equal(expected: "src/Example.slnx", actual: dotnet.Solution);
        Assert.Equal(expected: ["Example.Slow.Tests"], actual: dotnet.ExcludeTestProjects, comparer: System.StringComparer.Ordinal);
        Assert.Equal(expected: 45, actual: dotnet.TimeoutMinutes);
        Assert.Null(result.Overrides.Node);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"dotnet\": {\"command\": \"rm -rf /\"}}")]
    [InlineData("{\"unknown\": 1}")]
    [InlineData("null")]
    public async Task InvalidFileIsIgnoredWithAnErrorThatDoesNotEchoItAsync(string content)
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: OverridesLoader.RELATIVE_PATH, content: content, cancellationToken: this.CancellationToken());

        OverridesLoadResult result = await OverridesLoader.LoadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken());

        Assert.Same(expected: RepositoryOverrides.None, actual: result.Overrides);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain(expectedSubstring: "rm -rf", actualString: result.Error, comparisonType: System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedFileIsIgnoredAsync()
    {
        using TemporaryDirectory repository = new();
        await repository.WriteAsync(relative: OverridesLoader.RELATIVE_PATH, content: "{" + new string(c: ' ', count: 70 * 1024) + "}", cancellationToken: this.CancellationToken());

        OverridesLoadResult result = await OverridesLoader.LoadAsync(repositoryRoot: repository.Path, cancellationToken: this.CancellationToken());

        Assert.Same(expected: RepositoryOverrides.None, actual: result.Overrides);
        Assert.Contains(expectedSubstring: "64 KiB", actualString: result.Error, comparisonType: System.StringComparison.Ordinal);
    }
}
