using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Server.Tools;

public sealed class JobStatusTool : BuildTool
{
    private readonly SchemaProperty _id;
    private readonly IJobRunner _runner;

    public JobStatusTool(IJobRunner runner)
        : this(runner: runner, id: JobIdProperty.Create()) { }

    private JobStatusTool(IJobRunner runner, SchemaProperty id)
        : base(
            name: JobToolNames.JOB_STATUS,
            title: "Job status",
            description: "A single, instant look at whether a job is still running, how long it has run and which step it is on; to wait for a job, call job_result",
            schema: [id],
            readOnly: true
        )
    {
        this._runner = runner;
        this._id = id;
    }

    protected override ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        JobStatus status = this._runner.FindStatus(arguments.GetJobId(this._id)) ?? throw new JobNotFoundException();

        return ValueTask.FromResult(
            JsonSerializer.Serialize(value: status, jsonTypeInfo: CoreJsonContext.Default.JobStatus)
        );
    }
}
