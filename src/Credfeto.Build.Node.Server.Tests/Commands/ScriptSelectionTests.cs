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
        NodeRepository repository = SampleWorkspace.Create(ROOT);
        NodePackage web = repository.Packages[1];

        ScriptRun? run = TestScripts.For(repository: repository, package: web, scope: scope, overrideScript: null);

        Assert.NotNull(run);
        Assert.Equal(expected: expected, actual: run.Script);
        Assert.Same(expected: web, actual: run.Package);
        Assert.Null(run.PathFilter);
    }

    [Fact]
    public void UnitNeverFallsBackToAWorkspaceTestScript()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        Assert.Null(
            TestScripts.For(
                repository: repository,
                package: repository.Packages[2],
                scope: TestScripts.SCOPE_UNIT,
                overrideScript: null
            )
        );
    }

    [Fact]
    public void UnitRunsTheInstallRootNoE2eLimitedToTheWorkspace()
    {
        NodeRepository repository = SampleWorkspace.Create(
            repositoryRoot: ROOT,
            rootScripts: [NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
        );

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[2],
            scope: TestScripts.SCOPE_UNIT,
            overrideScript: null
        );

        Assert.NotNull(run);
        Assert.Same(expected: repository.Packages[0], actual: run.Package);
        Assert.Equal(expected: NodeScripts.TEST_NO_E2E, actual: run.Script);
        Assert.Equal(expected: "packages/core", actual: run.PathFilter);
    }

    [Fact]
    public void UnitPrefersTheWorkspaceOwnNoE2eOverTheInstallRoot()
    {
        NodeRepository repository = SampleWorkspace.Create(
            repositoryRoot: ROOT,
            rootScripts: [NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
        );

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[1],
            scope: TestScripts.SCOPE_UNIT,
            overrideScript: null
        );

        Assert.NotNull(run);
        Assert.Same(expected: repository.Packages[1], actual: run.Package);
        Assert.Equal(expected: NodeScripts.TEST_NO_E2E, actual: run.Script);
        Assert.Null(run.PathFilter);
    }

    [Fact]
    public void UnitFallsBackToTestForAnInstallRootWithoutNoE2e()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[0],
            scope: TestScripts.SCOPE_UNIT,
            overrideScript: null
        );

        Assert.NotNull(run);
        Assert.Equal(expected: NodeScripts.TEST, actual: run.Script);
        Assert.Null(run.PathFilter);
    }

    [Fact]
    public void UnitPrefersNoE2eForAnInstallRootThatHasIt()
    {
        NodeRepository repository = SampleWorkspace.Create(
            repositoryRoot: ROOT,
            rootScripts: [NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
        );

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[0],
            scope: TestScripts.SCOPE_UNIT,
            overrideScript: null
        );

        Assert.NotNull(run);
        Assert.Equal(expected: NodeScripts.TEST_NO_E2E, actual: run.Script);
        Assert.Null(run.PathFilter);
    }

    [Fact]
    public void AllScopeStillRunsAWorkspaceTestScript()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[2],
            scope: TestScripts.SCOPE_ALL,
            overrideScript: null
        );

        Assert.NotNull(run);
        Assert.Same(expected: repository.Packages[2], actual: run.Package);
        Assert.Equal(expected: NodeScripts.TEST, actual: run.Script);
    }

    [Fact]
    public void AWorkspaceUnderAnInstallRootWithoutAPackageHasNoUnitScript()
    {
        NodeRepository repository = new(
            RepositoryRoot: ROOT,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: "src", LockFile: "src/package-lock.json")],
            Packages:
            [
                new NodePackage(Name: "app", RelativeDirectory: "src/apps/app", Scripts: [NodeScripts.TEST])
                {
                    InstallRoot = "src",
                },
            ],
            Timeout: TimeSpan.FromMinutes(1)
        );

        Assert.Null(
            TestScripts.For(
                repository: repository,
                package: repository.Packages[0],
                scope: TestScripts.SCOPE_UNIT,
                overrideScript: null
            )
        );
    }

    [Fact]
    public void TheFilterIsRelativeToANestedInstallRoot()
    {
        NodeRepository repository = new(
            RepositoryRoot: ROOT,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: "src", LockFile: "src/package-lock.json")],
            Packages:
            [
                new NodePackage(Name: "source-root", RelativeDirectory: "src", Scripts: [NodeScripts.TEST_NO_E2E])
                {
                    InstallRoot = "src",
                },
                new NodePackage(Name: "app", RelativeDirectory: "src/apps/app", Scripts: [NodeScripts.TEST])
                {
                    InstallRoot = "src",
                },
            ],
            Timeout: TimeSpan.FromMinutes(1)
        );

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[1],
            scope: TestScripts.SCOPE_UNIT,
            overrideScript: null
        );

        Assert.NotNull(run);
        Assert.Same(expected: repository.Packages[0], actual: run.Package);
        Assert.Equal(expected: "apps/app", actual: run.PathFilter);
    }

    [Fact]
    public void TheOverrideOnlySelectsAScriptThePackageHas()
    {
        NodeRepository repository = SampleWorkspace.Create(
            repositoryRoot: ROOT,
            rootScripts: [NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
        );

        ScriptRun? run = TestScripts.For(
            repository: repository,
            package: repository.Packages[1],
            scope: TestScripts.SCOPE_UNIT,
            overrideScript: "test:e2e"
        );

        Assert.NotNull(run);
        Assert.Equal(expected: "test:e2e", actual: run.Script);
        Assert.Null(run.PathFilter);
        Assert.Null(
            TestScripts.For(
                repository: repository,
                package: repository.Packages[2],
                scope: TestScripts.SCOPE_UNIT,
                overrideScript: "test:e2e"
            )
        );
    }

    [Fact]
    public void TheNamedPackageIsChosenEvenWithoutTheScript()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        NodePackage package = Assert.Single(
            ScriptTargets.Packages(repository: repository, choice: SampleWorkspace.CORE, hasScript: static _ => false)
        );

        Assert.Equal(expected: SampleWorkspace.CORE, actual: package.Name);
    }
}
