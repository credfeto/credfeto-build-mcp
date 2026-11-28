using System;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Tools;

public sealed class WorkerLimitsTests : TestBase
{
    private static readonly ResourceNote TwoWorkers = new(
        Workers: 2,
        MemoryPerWorkerMb: 1536,
        MemoryLimitMb: 4096,
        MemoryLimitSource: "cgroup v2"
    );

    [Fact]
    public void JestGetsAWorkerCountAndAnIdleMemoryLimit()
    {
        Assert.Equal(
            expected: ["--maxWorkers=2", "--workerIdleMemoryLimit=1536MiB"],
            actual: WorkerLimits.Arguments(runner: NodeTestRunner.Jest, resources: TwoWorkers),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void VitestGetsAWorkerCount()
    {
        Assert.Equal(
            expected: ["--maxWorkers=2"],
            actual: WorkerLimits.Arguments(runner: NodeTestRunner.Vitest, resources: TwoWorkers),
            comparer: StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData(NodeTestRunner.NodeTest)]
    [InlineData(NodeTestRunner.Unknown)]
    public static void OtherRunnersGetNothing(NodeTestRunner runner)
    {
        Assert.Empty(WorkerLimits.Arguments(runner: runner, resources: TwoWorkers));
    }

    [Fact]
    public void TheWorkerArgumentsComeBeforeTheRunsOwn()
    {
        ScriptRun run = new(Package: Package(NodeTestRunner.Vitest), Script: NodeScripts.TEST)
        {
            RunnerArguments = ["--coverage", "src/a.test.ts"],
        };

        ScriptRun limited = WorkerLimits.Apply(run: run, resources: TwoWorkers);

        Assert.Equal(
            expected: ["--maxWorkers=2", "--coverage", "src/a.test.ts"],
            actual: limited.RunnerArguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void ARunnerThatTakesNoWorkerArgumentsIsLeftAsItWas()
    {
        ScriptRun run = new(Package: Package(NodeTestRunner.NodeTest), Script: NodeScripts.TEST);

        Assert.Same(expected: run, actual: WorkerLimits.Apply(run: run, resources: TwoWorkers));
    }

    [Theory]
    [InlineData(false, 1024)]
    [InlineData(true, 1536)]
    public static void TheBudgetFollowsTheKindOfRun(bool coverage, long expected)
    {
        Assert.Equal(
            expected: expected,
            actual: WorkerLimits.MemoryPerWorkerMb(repository: Repository(memoryPerWorkerMb: null), coverage: coverage)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void AnExplicitMemoryPerWorkerWins(bool coverage)
    {
        Assert.Equal(
            expected: 3072,
            actual: WorkerLimits.MemoryPerWorkerMb(repository: Repository(memoryPerWorkerMb: 3072), coverage: coverage)
        );
    }

    private static NodePackage Package(NodeTestRunner runner)
    {
        return new(Name: "app", RelativeDirectory: ".", Scripts: [NodeScripts.TEST]) { TestRunner = runner };
    }

    private static NodeRepository Repository(int? memoryPerWorkerMb)
    {
        return new(
            RepositoryRoot: "/repo",
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages: [Package(NodeTestRunner.Vitest)],
            Timeout: TimeSpan.FromMinutes(5)
        )
        {
            MemoryPerWorkerMb = memoryPerWorkerMb,
        };
    }
}
