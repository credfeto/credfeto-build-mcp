using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Core.Resources;

public sealed class MachineResources
{
    private readonly IMemorySource _memory;
    private readonly int _processorCount;

    public MachineResources(IMemorySource memory, int processorCount)
    {
        this._memory = memory;
        this._processorCount = processorCount;
    }

    // The limit is read afresh for every job, since a container's limit can be changed while the server runs
    public ResourceNote Plan(long memoryPerWorkerMb)
    {
        return WorkerBudget.Plan(
            processorCount: this._processorCount,
            limit: MemoryLimit.Detect(this._memory),
            memoryPerWorkerMb: memoryPerWorkerMb
        );
    }
}
