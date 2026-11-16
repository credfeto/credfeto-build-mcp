using System.Diagnostics;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs;

// A job's status, and a task that completes when the job next starts a step or finishes; taken together, so no change
// between reading the status and waiting for the next one is missed
[DebuggerDisplay("Id={Status.Id}, State={Status.State}")]
public sealed record JobWatch(JobStatus Status, Task Changed);
