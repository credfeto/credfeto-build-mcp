using System;
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
