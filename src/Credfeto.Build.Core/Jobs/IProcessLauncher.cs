using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs;

public interface IProcessLauncher
{
    /// <summary>
    ///     Runs the step to completion, passing every stdout and stderr line to <paramref name="onOutputLine" />, and
    ///     returns its exit code. Cancelling kills the process tree and throws <see cref="OperationCanceledException" />.
    /// </summary>
    ValueTask<int> RunAsync(ProcessStep step, Func<string, ValueTask> onOutputLine, CancellationToken cancellationToken);
}
