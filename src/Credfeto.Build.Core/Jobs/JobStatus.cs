using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("Id={Id}, Kind={Kind}, State={State}, StepsCompleted={StepsCompleted}/{StepCount}")]
public sealed record JobStatus(
    string Id,
    string Kind,
    JobState State,
    double DurationSeconds,
    int StepsCompleted,
    int StepCount,
    string? CurrentStep,
    string LogPath
);
