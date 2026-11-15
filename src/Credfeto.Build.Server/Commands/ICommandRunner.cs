using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Server.Commands;

// Runs a short command to completion and returns its output, for tools that answer at once rather than start a job
public interface ICommandRunner
{
    ValueTask<CommandOutput> RunAsync(ProcessStep step, TimeSpan timeout, CancellationToken cancellationToken);
}
