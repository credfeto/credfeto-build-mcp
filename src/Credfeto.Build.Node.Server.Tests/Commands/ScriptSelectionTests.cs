using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Commands;

public sealed class ScriptSelectionTests : TestBase
{
    private const string ROOT = "/work/repo";

    [Fact]
    public void AllRunsTheRootScriptOnceWhenTheRootHasIt()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        IReadOnlyList<(NodePackage Package, string Script)> targets = ScriptTargets.Select(
            repository: repository,
            choice: ScriptTargets.ALL,
            script: package => package.HasScript("test") ? "test" : null
        );

        (NodePackage package, string script) = Assert.Single(targets);
        Assert.Equal(expected: SampleWorkspace.ROOT_NAME, actual: package.Name);
        Assert.Equal(expected: "test", actual: script);
    }

    [Fact]
    public void AllRunsEachWorkspaceWhenTheRootLacksTheScript()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        IReadOnlyList<(NodePackage Package, string Script)> targets = ScriptTargets.Select(
            repository: repository,
            choice: ScriptTargets.ALL,
            script: package => package.HasScript("build") ? "build" : null
        );

        Assert.Equal(
            expected: [SampleWorkspace.WEB],
            actual: targets.Select(static target => target.Package.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void ANamedPackageRunsOnItsOwn()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        IReadOnlyList<(NodePackage Package, string Script)> targets = ScriptTargets.Select(
            repository: repository,
            choice: SampleWorkspace.CORE,
            script: package => package.HasScript("test") ? "test" : null
        );

        Assert.Equal(expected: SampleWorkspace.CORE, actual: Assert.Single(targets).Package.Name);
    }

    [Fact]
    public void TheChoicesAreAllThenTheSortedPackagesWithTheScript()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        IReadOnlyList<string> choices = ScriptTargets.Choices(repository: repository, script: package =>
            package.HasScript("test") ? "test" : null
        );

        Assert.Equal(
            expected: [ScriptTargets.ALL, SampleWorkspace.CORE, SampleWorkspace.WEB, SampleWorkspace.ROOT_NAME],
            actual: choices,
            comparer: StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData(TestScripts.SCOPE_UNIT, "test:noe2e")]
    [InlineData(TestScripts.SCOPE_ALL, "test")]
    public static void TheScopeChoosesBetweenTestAndNoE2e(string scope, string expected)
    {
        NodePackage web = SampleWorkspace.Create(ROOT).Packages[1];

        Assert.Equal(expected: expected, actual: TestScripts.For(package: web, scope: scope, overrideScript: null));
    }

    [Fact]
    public void UnitFallsBackToTestWithoutNoE2e()
    {
        NodePackage core = SampleWorkspace.Create(ROOT).Packages[2];

        Assert.Equal(
            expected: "test",
            actual: TestScripts.For(package: core, scope: TestScripts.SCOPE_UNIT, overrideScript: null)
        );
    }

    [Fact]
    public void TheOverrideOnlySelectsAScriptThePackageHas()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        Assert.Equal(
            expected: "test:e2e",
            actual: TestScripts.For(
                package: repository.Packages[1],
                scope: TestScripts.SCOPE_UNIT,
                overrideScript: "test:e2e"
            )
        );
        Assert.Null(
            TestScripts.For(package: repository.Packages[2], scope: TestScripts.SCOPE_UNIT, overrideScript: "test:e2e")
        );
    }
}
