using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Workspace;

namespace Credfeto.Build.Server.Tests.Helpers;

// Counts the watcher's calls; the first call can be made to fail as a repository caught mid-change would
internal sealed class RecordingChangeHandler : IRepositoryChangeHandler
{
    private int _calls;

    public bool FailFirstCall { get; init; }

    public int Calls => Volatile.Read(ref this._calls);

    public ValueTask RepositoryChangedAsync(CancellationToken cancellationToken)
    {
        int call = Interlocked.Increment(ref this._calls);

        return this.FailFirstCall && call == 1
            ? ValueTask.FromException(new InvalidDataException("mid-change"))
            : ValueTask.CompletedTask;
    }

    public bool IsTransientFailure(Exception exception)
    {
        return exception is InvalidDataException;
    }
}
