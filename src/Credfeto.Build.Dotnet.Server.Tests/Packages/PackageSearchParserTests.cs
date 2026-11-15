using System;
using System.Text.Json;
using Credfeto.Build.Dotnet.Server.Models;
using Credfeto.Build.Dotnet.Server.Packages;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Packages;

public sealed class PackageSearchParserTests : TestBase
{
    [Fact]
    public static void ReadsEachSourceAndOnlyTheRequestedId()
    {
        PackageSearchResult result = PackageSearchParser.Parse(
            json: PackageSearchFixtures.FOUND,
            packageId: "example.package"
        );

        Assert.Equal(expected: 3, actual: result.Sources.Count);
        Assert.Equal(expected: "Public", actual: result.Sources[1].Source);
        Assert.Equal(
            expected: ["1.0.0", "1.1.0", "1.2.0-rc.1", "1.2.0", "1.3.0"],
            actual: result.Sources[1].Versions,
            comparer: StringComparer.Ordinal
        );
        Assert.Empty(result.Sources[2].Versions);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public static void ReadsPerSourceProblems()
    {
        PackageSearchResult result = PackageSearchParser.Parse(
            json: PackageSearchFixtures.NOT_FOUND,
            packageId: "Example.Package"
        );

        PackageSourceProblem problem = Assert.Single(result.Problems);
        Assert.Equal(expected: "Broken", actual: problem.Source);
        Assert.StartsWith(
            expectedStartString: "Unable to load the service index",
            actualString: problem.Text,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(
        "Unable to load the service index for source https://user:s3cret@feed.example/v3/index.json?code=key.",
        "Unable to load the service index for source https://feed.example/v3/index.json"
    )]
    [InlineData(
        "Response status code 401 for https://feed.example/F/x/v3",
        "Response status code 401 for https://feed.example/F/x/v3"
    )]
    [InlineData("No URL here", "No URL here")]
    public static void ProblemsNeverCarryCredentialsInUrls(string text, string redacted)
    {
        string json =
            "{ \"searchResult\": [ { \"sourceName\": \"Feed\", \"problems\": [ { \"text\": \""
            + JsonEncodedText.Encode(text).ToString()
            + "\" } ], \"packages\": [] } ] }";

        PackageSearchResult result = PackageSearchParser.Parse(json: json, packageId: "Example.Package");

        Assert.Equal(expected: redacted, actual: Assert.Single(result.Problems).Text);
    }

    [Fact]
    public static void SkipsTextBeforeTheJson()
    {
        PackageSearchResult result = PackageSearchParser.Parse(
            json: "warning: something\n" + PackageSearchFixtures.FOUND,
            packageId: "Example.Package"
        );

        Assert.Equal(expected: 3, actual: result.Sources.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"searchResult\": [ { \"packages\": [] } ] }")]
    [InlineData("{ \"searchResult\": [ { \"sourceName\": ")]
    public static void UnreadableOutputIsAToolError(string json)
    {
        Assert.Throws<ToolUnavailableException>(() =>
            PackageSearchParser.Parse(json: json, packageId: "Example.Package")
        );
    }
}
