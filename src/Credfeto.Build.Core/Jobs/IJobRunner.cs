using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs;

public interface IJobRunner
{
    ValueTask<JobStatus> StartAsync(JobDefinition definition);

    JobStatus? FindStatus(string id);

    JobResult? FindResult(string id);

    // The job's status now, with a signal for its next change; null when the id is unknown
    JobWatch? Watch(string id);

    // Waits up to maxWait for the job to finish: true when it has, false when the wait ran out, the server is shutting
    // down or the id is unknown. Ending the wait, by cancellation or otherwise, never touches the job
    ValueTask<bool> WaitForCompletionAsync(string id, TimeSpan maxWait, CancellationToken cancellationToken);

    // Waits up to maxWait for the watched job to start its next step or finish
    ValueTask<JobWaitOutcome> WaitForChangeAsync(JobWatch watch, TimeSpan maxWait, CancellationToken cancellationToken);
}
