namespace Credfeto.Build.Core.Jobs;

public enum JobWaitOutcome
{
    // The job finished, or for a step watch, started its next step or finished
    Signalled,

    TimedOut,

    // The server is shutting down; the job is being stopped, so waiting on is pointless
    ShuttingDown,
}
