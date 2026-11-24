using Credfeto.Build.Python.Server.Commands;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Commands;

public sealed class RequiresPythonTests : TestBase
{
    [Theory]
    [InlineData(">=3.11", true)]
    [InlineData(">=3.14", false)]
    [InlineData(">=3.10,<4", true)]
    [InlineData(">=3.10, <3.13", false)]
    [InlineData("~=3.12", true)]
    [InlineData("~=3.13.2", false)]
    [InlineData("==3.13.*", true)]
    [InlineData("==3.12.*", false)]
    [InlineData("!=3.13.*", false)]
    [InlineData("==3.13.1", true)]
    [InlineData(">3.13", true)]
    [InlineData("<=3.13", false)]
    public static void TheSpecifierIsEvaluatedAgainstTheInterpreter(string specifiers, bool expected)
    {
        Assert.Equal(
            expected: expected,
            actual: RequiresPython.Allows(specifiers: specifiers, version: new(Major: 3, Minor: 13, Patch: 1))
        );
    }

    [Theory]
    [InlineData(">=3.11; os_name == 'nt'")]
    [InlineData("3.11")]
    [InlineData(">=3.11a1")]
    public static void ASpecifierThatCannotBeReadNeverBlocks(string specifiers)
    {
        Assert.Null(RequiresPython.Allows(specifiers: specifiers, version: new(Major: 3, Minor: 13, Patch: 1)));
    }

    [Theory]
    [InlineData("3.13.1\n", "3.13.1")]
    [InlineData("3.9.20", "3.9.20")]
    public static void TheVersionIsRead(string text, string expected)
    {
        Assert.Equal(expected: expected, actual: PythonVersion.Parse(text)?.Text);
    }

    [Theory]
    [InlineData("Python 3.13.1")]
    [InlineData("3.13")]
    [InlineData("")]
    public static void AnythingElseIsNoVersion(string text)
    {
        Assert.Null(PythonVersion.Parse(text));
    }
}
