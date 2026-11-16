using Credfeto.Build.Node.Server.Detection;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Detection;

public sealed class TestRunnerInferenceTests : TestBase
{
    [Theory]
    [InlineData("vitest run", NodeTestRunner.Vitest)]
    [InlineData("vitest", NodeTestRunner.Vitest)]
    [InlineData("CI=true vitest run --coverage", NodeTestRunner.Vitest)]
    [InlineData("jest --ci", NodeTestRunner.Jest)]
    [InlineData("node --test", NodeTestRunner.NodeTest)]
    [InlineData("node --test --test-reporter=tap test/", NodeTestRunner.NodeTest)]
    [InlineData("node index.js", NodeTestRunner.Unknown)]
    [InlineData("mocha", NodeTestRunner.Unknown)]
    public static void TheCommandNamesTheRunner(string command, NodeTestRunner expected)
    {
        Assert.Equal(expected: expected, actual: TestRunnerInference.Infer(testCommand: command, dependencies: []));
    }

    [Fact]
    public void TheDependenciesNameTheRunnerWhenTheCommandDoesNot()
    {
        Assert.Equal(
            expected: NodeTestRunner.Jest,
            actual: TestRunnerInference.Infer(
                testCommand: "npm run unit",
                dependencies: [new NodeDependency(Name: "jest", Section: DependencySection.DevDependencies)]
            )
        );
        Assert.Equal(
            expected: NodeTestRunner.Vitest,
            actual: TestRunnerInference.Infer(
                testCommand: null,
                dependencies: [new NodeDependency(Name: "vitest", Section: DependencySection.DevDependencies)]
            )
        );
    }
}
