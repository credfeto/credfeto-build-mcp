using System;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Core.Resources;

public static class WorkerBudget
{
    // The memory one test worker is allowed: an instrumented coverage run holds far more than a plain test run
    public const long COVERAGE_MEMORY_PER_WORKER_MB = 1536;
    public const long TEST_MEMORY_PER_WORKER_MB = 1024;

    private const long BYTES_PER_MIB = 1024 * 1024;

    // As many workers as the memory holds, never fewer than one and never more than there are processors; an unknown
    // limit leaves the processor count, which is what the test runners choose themselves
    public static int Workers(int processorCount, MemoryLimit? limit, long memoryPerWorkerMb)
    {
        int processors = Math.Max(val1: 1, val2: processorCount);

        if (limit is null || memoryPerWorkerMb <= 0)
        {
            return processors;
        }

        long byMemory = limit.Bytes / (memoryPerWorkerMb * BYTES_PER_MIB);

        return (int)Math.Clamp(value: byMemory, min: 1, max: processors);
    }

    public static ResourceNote Plan(int processorCount, MemoryLimit? limit, long memoryPerWorkerMb)
    {
        return new(
            Workers: Workers(processorCount: processorCount, limit: limit, memoryPerWorkerMb: memoryPerWorkerMb),
            MemoryPerWorkerMb: memoryPerWorkerMb,
            MemoryLimitMb: limit?.Megabytes,
            MemoryLimitSource: limit?.Source
        );
    }
}
