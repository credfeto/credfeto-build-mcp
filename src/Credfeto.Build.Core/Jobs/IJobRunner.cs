using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs;

public interface IJobRunner
{
    /// <summary>Starts a job in the background; only one job runs at a time.</summary>
    ValueTask<JobStatus> StartAsync(JobDefinition definition);

    JobStatus? FindStatus(string id);

    JobResult? FindResult(string id);

    ValueTask WaitForCompletionAsync(string id, CancellationToken cancellationToken);
}
