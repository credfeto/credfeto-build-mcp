using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Tools;

public sealed class ManifestCreatorTests : TestBase
{
    [Theory]
    [InlineData("credfeto-dns", "credfeto-dns")]
    [InlineData("Credfeto_AI.Skills", "credfeto-ai-skills")]
    [InlineData("", "tests")]
    [InlineData("_", "tests")]
    public static void TheNameIsANormalName(string source, string expected)
    {
        Assert.Equal(expected: expected, actual: ManifestCreator.Normalise(source));
    }

    [Fact]
    public void TheCreatedPyprojectIsDetectedAsAProjectThatDeclaresItsTestTools()
    {
        string text = ManifestCreator.Pyproject(name: "credfeto-dns", testSuite: new(["scripts/tests"]));

        (PyprojectManifest? manifest, string? problem) = PyprojectReader.Parse(
            text: text,
            displayPath: "pyproject.toml"
        );

        Assert.Null(problem);
        Assert.NotNull(manifest);
        Assert.Equal(expected: "credfeto-dns", actual: manifest.Name);
        Assert.Empty(manifest.Dependencies);
        Assert.True(condition: manifest.OptionalDependencies.ContainsKey("test"), userMessage: "a test extra");
    }
}
