using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

// The memory a job ran within and the worker count chosen for it, so an out-of-memory kill can be explained; the
// limit is null when none could be read
[DebuggerDisplay("Workers={Workers}, MemoryPerWorkerMb={MemoryPerWorkerMb}, MemoryLimitMb={MemoryLimitMb}")]
public sealed record ResourceNote(int Workers, long MemoryPerWorkerMb, long? MemoryLimitMb, string? MemoryLimitSource);
