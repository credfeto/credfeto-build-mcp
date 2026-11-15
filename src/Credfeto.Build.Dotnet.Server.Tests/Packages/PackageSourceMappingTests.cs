using System.Threading.Tasks;
using System.Xml.Linq;
using Credfeto.Build.Dotnet.Server.Packages;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Packages;

public sealed class PackageSourceMappingTests : TestBase
{
    private const string USER_CONFIG = """
        <configuration>
          <packageSources>
            <add key="Public" value="https://public.example/v3/index.json" />
            <add key="Private" value="https://private.example/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="Public">
              <package pattern="*" />
              <package pattern="Example.Test.*" />
              <package pattern="Example.Tool" />
            </packageSource>
            <packageSource key="Private">
              <package pattern="Example.*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

    private const string CLEARING_REPOSITORY_CONFIG = """
        <configuration>
          <packageSourceMapping>
            <clear />
            <packageSource key="Private">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

    private const string NO_MAPPING =
        """<configuration><packageSources><add key="Public" value="https://public.example/v3/index.json" /></packageSources></configuration>""";

    [Theory]
    [InlineData("Newtonsoft.Json", "Public", true)]
    [InlineData("Newtonsoft.Json", "Private", false)]
    [InlineData("Example.Common", "Private", true)]
    [InlineData("Example.Common", "Public", false)]
    [InlineData("Example.Test.Common", "Public", true)]
    [InlineData("Example.Test.Common", "Private", false)]
    [InlineData("example.tool", "Public", true)]
    [InlineData("Example.Tool", "Private", false)]
    [InlineData("Example.Common", "Unmapped", false)]
    public static void TheMostSpecificPatternPicksTheSources(string packageId, string source, bool allowed)
    {
        PackageSourceMapping mapping = PackageSourceMapping.FromDocuments([XDocument.Parse(USER_CONFIG)]);

        Assert.True(condition: mapping.Configured, userMessage: "the mapping is read");
        Assert.Equal(expected: allowed, actual: mapping.Allows(source: source, packageId: packageId));
    }

    [Fact]
    public static void WithoutAMappingEverySourceIsUsed()
    {
        PackageSourceMapping mapping = PackageSourceMapping.FromDocuments([XDocument.Parse(NO_MAPPING)]);

        Assert.False(condition: mapping.Configured, userMessage: "no packageSourceMapping section");
        Assert.True(
            condition: mapping.Allows(source: "Anything", packageId: "Example.Common"),
            userMessage: "every source is used"
        );
    }

    [Fact]
    public static void ACloserClearReplacesTheInheritedMapping()
    {
        PackageSourceMapping mapping = PackageSourceMapping.FromDocuments([
            XDocument.Parse(USER_CONFIG),
            XDocument.Parse(CLEARING_REPOSITORY_CONFIG),
        ]);

        Assert.True(
            condition: mapping.Allows(source: "Private", packageId: "Newtonsoft.Json"),
            userMessage: "the repository maps everything to Private"
        );
        Assert.False(
            condition: mapping.Allows(source: "Public", packageId: "Example.Test.Common"),
            userMessage: "the user mapping was cleared"
        );
    }

    [Fact]
    public static void PatternsMatchingNothingMeanNoSource()
    {
        const string config = """
            <configuration><packageSourceMapping><packageSource key="Private"><package pattern="Example.*" /></packageSource></packageSourceMapping></configuration>
            """;
        PackageSourceMapping mapping = PackageSourceMapping.FromDocuments([XDocument.Parse(config)]);

        Assert.False(
            condition: mapping.Allows(source: "Private", packageId: "Newtonsoft.Json"),
            userMessage: "restore finds no source for the id"
        );
    }

    [Fact]
    public async Task LoadsTheFilesClosestFirstAndSkipsMissingOrBrokenOnesAsync()
    {
        using TemporaryDirectory directory = new();
        string user = await directory.WriteAsync(
            relative: "user/NuGet.Config",
            content: USER_CONFIG,
            cancellationToken: this.CancellationToken()
        );
        string repository = await directory.WriteAsync(
            relative: "repo/nuget.config",
            content: CLEARING_REPOSITORY_CONFIG,
            cancellationToken: this.CancellationToken()
        );
        string broken = await directory.WriteAsync(
            relative: "broken/nuget.config",
            content: "<configuration>",
            cancellationToken: this.CancellationToken()
        );

        PackageSourceMapping mapping = await PackageSourceMapping.LoadAsync(
            configFiles: [repository, broken, directory.Combine("missing/nuget.config"), user],
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: mapping.Allows(source: "Private", packageId: "Newtonsoft.Json"),
            userMessage: "the closest file's clear wins"
        );
        Assert.False(
            condition: mapping.Allows(source: "Public", packageId: "Newtonsoft.Json"),
            userMessage: "the user mapping was cleared"
        );
    }
}
