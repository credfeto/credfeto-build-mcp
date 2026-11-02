using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

/// <summary>Records the jobs a tool starts instead of running them.</summary>
internal sealed class RecordingJobRunner : IJobRunner
{
    public const string JOB_ID = "0123456789abcdef0123456789abcdef";

    private readonly List<JobDefinition> _started = [];
    private readonly Dictionary<string, JobStatus> _statuses = new(StringComparer.Ordinal);

    public IReadOnlyList<JobDefinition> Started => this._started;

    public bool Busy { get; set; }

    public void Know(JobStatus status)
    {
        this._statuses[status.Id] = status;
    }

    public ValueTask<JobStatus> StartAsync(JobDefinition definition)
    {
        if (this.Busy)
        {
            throw new JobAlreadyRunningException();
        }

        this._started.Add(definition);

        return ValueTask.FromResult(
            new JobStatus(
                Id: JOB_ID,
                Kind: definition.Kind,
                State: JobState.Running,
                DurationSeconds: 0,
                StepsCompleted: 0,
                StepCount: definition.Steps.Count,
                CurrentStep: null,
                LogPath: "/logs/" + JOB_ID + ".log"
            )
        );
    }

    public JobStatus? FindStatus(string id)
    {
        return this._statuses.GetValueOrDefault(id);
    }

    public JobResult? FindResult(string id)
    {
        return null;
    }

    public ValueTask WaitForCompletionAsync(string id, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
