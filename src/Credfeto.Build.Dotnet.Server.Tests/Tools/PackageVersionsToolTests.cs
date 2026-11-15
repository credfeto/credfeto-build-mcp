using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tests.Packages;
using Credfeto.Build.Dotnet.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Tools;

public sealed class PackageVersionsToolTests : TestBase, IDisposable
{
    private const string MAPPING = """
        <configuration>
          <packageSourceMapping>
            <packageSource key="Public"><package pattern="*" /></packageSource>
            <packageSource key="Private"><package pattern="Other.*" /></packageSource>
          </packageSourceMapping>
        </configuration>
        """;

    private readonly FakeCommandRunner _commands = new FakeCommandRunner().Answer(
        stepName: "nuget config paths",
        standardOutput: string.Empty
    );
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task SearchesTheConfiguredSourcesFromTheSolutionDirectoryAsync()
    {
        this._commands.Answer(stepName: "package search", standardOutput: PackageSearchFixtures.FOUND);

        CallToolResult result = await this.CallAsync(Arguments(packageId: "Example.Package"));

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep search = this._commands.Steps[0];
        Assert.Equal(expected: "dotnet", actual: search.FileName);
        Assert.Equal(
            expected: ["package", "search", "Example.Package", "--exact-match", "--format", "json", "--prerelease"],
            actual: search.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: this._repository.Combine("src"), actual: search.WorkingDirectory);
        Assert.Equal(
            expected: ["nuget", "config", "paths"],
            actual: this._commands.Steps[1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task ListsEachSourceNewestFirstWithItsLatestVersionsAsync()
    {
        this._commands.Answer(stepName: "package search", standardOutput: PackageSearchFixtures.FOUND);

        using JsonDocument response = await this.CallForJsonAsync(Arguments(packageId: "Example.Package", take: 2));
        JsonElement root = response.RootElement;

        Assert.False(
            condition: root.GetProperty("sourceMappingConfigured").GetBoolean(),
            userMessage: "no config files were listed"
        );
        Assert.Equal(expected: "1.3.0", actual: root.GetProperty("latestStable").GetString());
        Assert.Equal(expected: "1.10.0-beta.1", actual: root.GetProperty("latestPrerelease").GetString());

        JsonElement[] sources = [.. root.GetProperty("sources").EnumerateArray()];
        Assert.Equal(
            expected: ["Private", "Public"],
            actual: sources.Select(static source => source.GetProperty("source").GetString()),
            comparer: StringComparer.Ordinal
        );
        JsonElement publicSource = sources[1];
        Assert.Equal(expected: 5, actual: publicSource.GetProperty("versionCount").GetInt32());
        Assert.Equal(
            expected: ["1.3.0", "1.2.0"],
            actual: Strings(publicSource.GetProperty("versions")),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "1.2.0-rc.1", actual: publicSource.GetProperty("latestPrerelease").GetString());
        Assert.True(
            condition: publicSource.GetProperty("usedByRestore").GetBoolean(),
            userMessage: "no mapping, every source is used"
        );
    }

    [Fact]
    public async Task WithoutPrereleasesTheSearchAndTheListLeaveThemOutAsync()
    {
        this._commands.Answer(stepName: "package search", standardOutput: PackageSearchFixtures.FOUND);

        using JsonDocument response = await this.CallForJsonAsync(
            Arguments(packageId: "Example.Package", includePrerelease: false)
        );

        Assert.DoesNotContain(
            expected: "--prerelease",
            collection: this._commands.Steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.False(
            condition: response.RootElement.TryGetProperty(propertyName: "latestPrerelease", value: out _),
            userMessage: "no prerelease listed"
        );
        Assert.Equal(
            expected: ["1.2.0"],
            actual: Strings(response.RootElement.GetProperty("sources")[0].GetProperty("versions")),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task TheMappingDecidesWhichSourcesCountAsync()
    {
        string config = await this._repository.WriteAsync(
            relative: "nuget.config",
            content: MAPPING,
            cancellationToken: this.CancellationToken()
        );
        this._commands.Answer(stepName: "package search", standardOutput: PackageSearchFixtures.FOUND)
            .Answer(stepName: "nuget config paths", standardOutput: config + "\n");

        using JsonDocument response = await this.CallForJsonAsync(Arguments(packageId: "Example.Package"));
        JsonElement root = response.RootElement;

        Assert.True(
            condition: root.GetProperty("sourceMappingConfigured").GetBoolean(),
            userMessage: "the mapping is read"
        );
        Assert.False(
            condition: root.GetProperty("sources")[0].GetProperty("usedByRestore").GetBoolean(),
            userMessage: "Private is mapped to Other.* only"
        );
        Assert.Equal(expected: "1.2.0-rc.1", actual: root.GetProperty("latestPrerelease").GetString());
    }

    [Fact]
    public async Task APackageOnNoSourceIsAToolErrorAsync()
    {
        this._commands.Answer(stepName: "package search", standardOutput: PackageSearchFixtures.NOT_FOUND);

        CallToolResult result = await this.CallAsync(Arguments(packageId: "Missing.Package"));

        Assert.True(condition: result.IsError, userMessage: "nothing was found");
        Assert.Equal(
            expected: "No configured package source lists Missing.Package (searched: Public, Broken); these sources could not be searched: Broken",
            actual: Text(result)
        );
    }

    [Theory]
    [InlineData(1, false, "dotnet package search failed with exit code 1")]
    [InlineData(-1, true, "dotnet package search did not finish within 90 seconds")]
    public async Task AFailedSearchIsAToolErrorAsync(int exitCode, bool timedOut, string message)
    {
        this._commands.Answer(
            stepName: "package search",
            output: new CommandOutput(ExitCode: exitCode, StandardOutput: string.Empty, TimedOut: timedOut)
        );

        CallToolResult result = await this.CallAsync(Arguments(packageId: "Example.Package"));

        Assert.True(condition: result.IsError, userMessage: "the search failed");
        Assert.Equal(expected: message, actual: Text(result));
    }

    [Theory]
    [InlineData("--source")]
    [InlineData("Example Package")]
    [InlineData("")]
    [InlineData("Example/../Package")]
    public async Task APackageIdMustLookLikeOneAsync(string packageId)
    {
        CallToolResult result = await this.CallAsync(Arguments(packageId: packageId));

        Assert.True(condition: result.IsError, userMessage: "refused " + packageId);
        Assert.Empty(this._commands.Steps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PackageVersionsTool.MAX_TAKE + 1)]
    public async Task TakeMustBeInRangeAsync(int take)
    {
        CallToolResult result = await this.CallAsync(Arguments(packageId: "Example.Package", take: take));

        Assert.True(condition: result.IsError, userMessage: "take out of range");
        Assert.Empty(this._commands.Steps);
    }

    [Fact]
    public static void ConfigFilesAreTheAbsolutePathsListed()
    {
        Assert.Equal(
            expected: ["/repo/nuget.config", "/home/user/.nuget/NuGet/NuGet.Config"],
            actual: PackageVersionsTool.ConfigFiles(
                " /repo/nuget.config\r\n\nnot/a/path\n/home/user/.nuget/NuGet/NuGet.Config\n"
            ),
            comparer: StringComparer.Ordinal
        );
    }

    private async ValueTask<JsonDocument> CallForJsonAsync(Dictionary<string, JsonElement> arguments)
    {
        CallToolResult result = await this.CallAsync(arguments);
        Assert.False(condition: result.IsError, userMessage: Text(result));

        return JsonDocument.Parse(Text(result));
    }

    private ValueTask<CallToolResult> CallAsync(Dictionary<string, JsonElement> arguments)
    {
        DotnetDetection detection = TestRepositories.Detected(
            TestRepositories.Create(root: this._repository.Path, TestRepositories.CoreTests)
        );
        ToolContext context = TestContexts.Create(
            runner: new RecordingJobRunner(),
            commands: this._commands,
            detection: detection,
            buildtest: TestContexts.NoBuildtest
        );

        return TestContexts
            .Find(tools: DotnetToolCatalog.Create(detection: detection, context: context), name: "package_versions")
            .CallAsync(arguments: arguments, cancellationToken: this.CancellationToken());
    }

    private static Dictionary<string, JsonElement> Arguments(
        string packageId,
        int take = PackageVersionsTool.DEFAULT_TAKE,
        bool includePrerelease = true
    )
    {
        return new(StringComparer.Ordinal)
        {
            ["package_id"] = JsonSerializer.SerializeToElement(
                value: packageId,
                jsonTypeInfo: TestJsonContext.Default.String
            ),
            ["take"] = JsonSerializer.SerializeToElement(value: take, jsonTypeInfo: TestJsonContext.Default.Int32),
            ["include_prerelease"] = JsonSerializer.SerializeToElement(
                value: includePrerelease,
                jsonTypeInfo: TestJsonContext.Default.Boolean
            ),
        };
    }

    private static IEnumerable<string> Strings(JsonElement array)
    {
        return array.EnumerateArray().Select(static value => value.GetString() ?? string.Empty);
    }

    private static string Text(CallToolResult result)
    {
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }
}
