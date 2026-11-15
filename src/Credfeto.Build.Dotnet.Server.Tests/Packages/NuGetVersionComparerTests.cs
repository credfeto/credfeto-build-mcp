using System;
using System.Linq;
using Credfeto.Build.Dotnet.Server.Packages;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Packages;

public sealed class NuGetVersionComparerTests : TestBase
{
    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.0", "1.0.0.1")]
    [InlineData("1.0.0-beta", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10")]
    [InlineData("1.0.0-beta", "1.0.0-beta.1")]
    [InlineData("1.0.0-1", "1.0.0-alpha")]
    [InlineData("1.6.0-beta.7.gbbd7fe56aa", "1.6.0")]
    [InlineData("not-a-version", "0.0.1")]
    [InlineData("1.2.3.4.5", "0.0.1")]
    [InlineData("not-a-version", "v1.0.0")]
    public static void OrdersVersionsTheNuGetWay(string lower, string higher)
    {
        Assert.True(
            condition: NuGetVersionComparer.Instance.Compare(x: lower, y: higher) < 0,
            userMessage: lower + " < " + higher
        );
        Assert.True(
            condition: NuGetVersionComparer.Instance.Compare(x: higher, y: lower) > 0,
            userMessage: higher + " > " + lower
        );
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0+build.5")]
    [InlineData("1.0.0-RC.1", "1.0.0-rc.1")]
    [InlineData("1.0", "1.0.0.0")]
    public static void TreatsEquivalentVersionsAsEqual(string x, string y)
    {
        Assert.Equal(expected: 0, actual: NuGetVersionComparer.Instance.Compare(x: x, y: y));
    }

    [Theory]
    [InlineData("1.0.0-beta", true)]
    [InlineData("1.0.1.15-master", true)]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.0+meta-data", false)]
    public static void KnowsAPrerelease(string version, bool prerelease)
    {
        Assert.Equal(expected: prerelease, actual: NuGetVersionComparer.IsPrerelease(version));
    }

    [Fact]
    public static void SortsNewestFirst()
    {
        string[] versions = ["1.0.0", "2.0.0-rc.1", "1.10.0", "2.0.0", "1.9.9"];
        string[] sorted = [.. versions.OrderDescending(NuGetVersionComparer.Instance)];

        Assert.Equal(
            expected: ["2.0.0", "2.0.0-rc.1", "1.10.0", "1.9.9", "1.0.0"],
            actual: sorted,
            comparer: StringComparer.Ordinal
        );
    }
}
