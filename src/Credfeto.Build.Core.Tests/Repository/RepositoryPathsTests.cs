using Credfeto.Build.Core.Repository;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class RepositoryPathsTests : TestBase
{
    [Theory]
    [InlineData("/repo", "/repo/src/a.slnx", true)]
    [InlineData("/repo", "/repo", true)]
    [InlineData("/repo", "/repo/../etc/passwd", false)]
    [InlineData("/repo", "/other/a.slnx", false)]
    [InlineData("/repo", "/repository/a.slnx", false)]
    public static void IsWithinOnlyAcceptsPathsUnderTheRoot(string root, string path, bool expected)
    {
        Assert.Equal(expected: expected, actual: RepositoryPaths.IsWithin(root: root, path: System.IO.Path.GetFullPath(path)));
    }

    [Fact]
    public static void RelativeToUsesForwardSlashes()
    {
        Assert.Equal(expected: "src/A.Tests/A.Tests.csproj", actual: RepositoryPaths.RelativeTo(root: "/repo", path: "/repo/src/A.Tests/A.Tests.csproj"));
    }

    [Fact]
    public static void WithTrailingSeparatorAddsOnlyOne()
    {
        Assert.Equal(expected: "/repo/", actual: RepositoryPaths.WithTrailingSeparator("/repo"));
        Assert.Equal(expected: "/repo/", actual: RepositoryPaths.WithTrailingSeparator("/repo/"));
    }
}
