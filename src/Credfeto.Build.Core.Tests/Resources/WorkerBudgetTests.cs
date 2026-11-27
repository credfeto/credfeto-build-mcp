using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Resources;

public sealed class WorkerBudgetTests : TestBase
{
    private const long ONE_GIB = 1024L * 1024 * 1024;

    private static readonly MemoryLimit FourGigabytes = new(Bytes: 4 * ONE_GIB, Source: "cgroup v2");

    [Fact]
    public void CoverageInAFourGigabyteContainerRunsTwoWorkers()
    {
        Assert.Equal(
            expected: 2,
            actual: WorkerBudget.Workers(
                processorCount: 8,
                limit: FourGigabytes,
                memoryPerWorkerMb: WorkerBudget.COVERAGE_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Fact]
    public void TestsInAFourGigabyteContainerRunFourWorkers()
    {
        Assert.Equal(
            expected: 4,
            actual: WorkerBudget.Workers(
                processorCount: 8,
                limit: FourGigabytes,
                memoryPerWorkerMb: WorkerBudget.TEST_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Fact]
    public void TooLittleMemoryStillRunsOneWorker()
    {
        Assert.Equal(
            expected: 1,
            actual: WorkerBudget.Workers(
                processorCount: 8,
                limit: new(Bytes: 512L * 1024 * 1024, Source: "cgroup v2"),
                memoryPerWorkerMb: WorkerBudget.COVERAGE_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Fact]
    public void PlentyOfMemoryNeverRunsMoreWorkersThanProcessors()
    {
        Assert.Equal(
            expected: 8,
            actual: WorkerBudget.Workers(
                processorCount: 8,
                limit: new(Bytes: 256 * ONE_GIB, Source: "MemAvailable"),
                memoryPerWorkerMb: WorkerBudget.TEST_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Fact]
    public void FewerProcessorsThanTheMemoryHoldsWin()
    {
        Assert.Equal(
            expected: 2,
            actual: WorkerBudget.Workers(
                processorCount: 2,
                limit: FourGigabytes,
                memoryPerWorkerMb: WorkerBudget.TEST_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Fact]
    public void AnUnknownLimitRunsAWorkerPerProcessor()
    {
        Assert.Equal(
            expected: 6,
            actual: WorkerBudget.Workers(
                processorCount: 6,
                limit: null,
                memoryPerWorkerMb: WorkerBudget.TEST_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void ABudgetThatIsNotPositiveRunsAWorkerPerProcessor(long memoryPerWorkerMb)
    {
        Assert.Equal(
            expected: 6,
            actual: WorkerBudget.Workers(processorCount: 6, limit: FourGigabytes, memoryPerWorkerMb: memoryPerWorkerMb)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public static void NoProcessorCountStillRunsOneWorker(int processorCount)
    {
        Assert.Equal(
            expected: 1,
            actual: WorkerBudget.Workers(
                processorCount: processorCount,
                limit: null,
                memoryPerWorkerMb: WorkerBudget.TEST_MEMORY_PER_WORKER_MB
            )
        );
    }

    [Fact]
    public void ThePlanRecordsTheLimitAndTheBudget()
    {
        Assert.Equal(
            expected: new ResourceNote(
                Workers: 2,
                MemoryPerWorkerMb: 1536,
                MemoryLimitMb: 4096,
                MemoryLimitSource: "cgroup v2"
            ),
            actual: WorkerBudget.Plan(processorCount: 8, limit: FourGigabytes, memoryPerWorkerMb: 1536)
        );
    }

    [Fact]
    public void ThePlanOfAnUnknownLimitHasNoLimit()
    {
        Assert.Equal(
            expected: new ResourceNote(
                Workers: 3,
                MemoryPerWorkerMb: 1024,
                MemoryLimitMb: null,
                MemoryLimitSource: null
            ),
            actual: WorkerBudget.Plan(processorCount: 3, limit: null, memoryPerWorkerMb: 1024)
        );
    }

    [Fact]
    public void TheMachinesPlanReadsItsMemoryLimit()
    {
        MachineResources machine = new(memory: FixedMemorySource.CgroupV2(4 * ONE_GIB), processorCount: 8);

        Assert.Equal(
            expected: new ResourceNote(
                Workers: 4,
                MemoryPerWorkerMb: 1024,
                MemoryLimitMb: 4096,
                MemoryLimitSource: "cgroup v2"
            ),
            actual: machine.Plan(WorkerBudget.TEST_MEMORY_PER_WORKER_MB)
        );
    }
}
