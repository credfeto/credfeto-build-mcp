using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Server.Workspace;

// What the repository watcher calls once a burst of relevant changes has settled
public interface IRepositoryChangeHandler
{
    ValueTask RepositoryChangedAsync(CancellationToken cancellationToken);

    // A failure that only means the repository is mid-change, so the next event or tool call looks again
    bool IsTransientFailure(Exception exception);
}
