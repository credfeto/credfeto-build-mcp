using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs;

public interface IJobRunner
{
    ValueTask<JobStatus> StartAsync(JobDefinition definition);

    JobStatus? FindStatus(string id);

    JobResult? FindResult(string id);

    // True once the job has finished; false for an unknown job, or one still running when the wait runs out
    ValueTask<bool> WaitForCompletionAsync(string id, TimeSpan maxWait, CancellationToken cancellationToken);

    // Returns once the job has completed a step beyond stepsCompleted, begun another or finished, or the wait runs
    // out; true once there is nothing more to wait for (the job has finished or the server is shutting down), false
    // for an unknown job or one still running
    ValueTask<bool> WaitForChangeAsync(
        string id,
        int stepsCompleted,
        TimeSpan maxWait,
        CancellationToken cancellationToken
    );
}
