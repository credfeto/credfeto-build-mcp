using System;
using Credfeto.Build.Python.Server.Detection;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Detection;

public sealed class RequirementNameTests : TestBase
{
    [Theory]
    [InlineData("PyTest>=8", "pytest")]
    [InlineData("coverage[toml]>=7.4", "coverage")]
    [InlineData("  Foo_Bar.baz ; python_version < '3.12'", "foo-bar-baz")]
    [InlineData("httpx @ https://example.com/httpx.whl", "httpx")]
    [InlineData("zope..interface", "zope-interface")]
    public static void TheNameIsNormalised(string requirement, string expected)
    {
        Assert.Equal(expected: expected, actual: RequirementName.Of(requirement));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-r requirements.txt")]
    [InlineData("_private")]
    public static void TextThatNamesNoDistributionHasNoName(string requirement)
    {
        Assert.Null(RequirementName.Of(requirement));
    }

    [Fact]
    public void RequirementsFilesSkipCommentsBlankLinesAndOptions()
    {
        const string text =
            "# comment\n\n-r base.txt\n--index-url https://example.com\npytest==8.3 # pinned\nurl#fragment\r\n";

        Assert.Equal(
            expected: ["pytest==8.3", "url#fragment"],
            actual: RequirementsReader.Parse(text),
            comparer: StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData("requirements.txt", true)]
    [InlineData("requirements-dev.txt", true)]
    [InlineData("requirements_test.txt", true)]
    [InlineData("dev-requirements.txt", false)]
    [InlineData("requirements.in", false)]
    public static void OnlyRequirementsStarTxtIsARequirementsFile(string fileName, bool expected)
    {
        Assert.Equal(expected: expected, actual: RequirementsReader.IsRequirementsFile(fileName));
    }

    [Theory]
    [InlineData("test_parser.py", true)]
    [InlineData("parser_test.py", true)]
    [InlineData("parser.py", false)]
    [InlineData("conftest.py", false)]
    [InlineData("test_data.json", false)]
    public static void TestFilesAreNamedAsPytestCollectsThem(string fileName, bool expected)
    {
        Assert.Equal(expected: expected, actual: TestSuiteScan.IsTestFile(fileName));
    }
}
