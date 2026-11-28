using System.Collections.Generic;
using System.Globalization;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Tools;

// Caps a test run's workers to what the memory limit holds, so a small container does not get its runner killed for
// starting one worker per processor. Only Jest and Vitest are known to take these switches; any other runner's
// arguments are left exactly as they were
public static class WorkerLimits
{
    // An explicit node.memoryPerWorkerMb wins; otherwise the budget for the kind of run
    public static long MemoryPerWorkerMb(NodeRepository repository, bool coverage)
    {
        return repository.MemoryPerWorkerMb
            ?? (coverage ? WorkerBudget.COVERAGE_MEMORY_PER_WORKER_MB : WorkerBudget.TEST_MEMORY_PER_WORKER_MB);
    }

    public static ScriptRun Apply(ScriptRun run, ResourceNote resources)
    {
        IReadOnlyList<string> arguments = Arguments(runner: run.Package.TestRunner, resources: resources);

        return arguments.Count == 0 ? run : run with { RunnerArguments = [.. arguments, .. run.RunnerArguments] };
    }

    public static IReadOnlyList<string> Arguments(NodeTestRunner runner, ResourceNote resources)
    {
        string workers = "--maxWorkers=" + resources.Workers.ToString(CultureInfo.InvariantCulture);

        return runner switch
        {
            // Jest also recycles a worker whose heap has grown past its budget, since its workers leak between files
            NodeTestRunner.Jest =>
            [
                workers,
                "--workerIdleMemoryLimit=" + resources.MemoryPerWorkerMb.ToString(CultureInfo.InvariantCulture) + "MiB",
            ],
            NodeTestRunner.Vitest => [workers],
            _ => [],
        };
    }
}
