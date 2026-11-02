using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class JobStatusTool : BuildTool
{
    private readonly SchemaProperty _id;
    private readonly IJobRunner _runner;

    public JobStatusTool(IJobRunner runner)
        : this(runner: runner, id: JobIdProperty.Create()) { }

    private JobStatusTool(IJobRunner runner, SchemaProperty id)
        : base(
            name: ToolNames.JOB_STATUS,
            title: "Job status",
            description: "Reports whether a job is still running, how long it has run and which step it is on",
            schema: [id],
            readOnly: true
        )
    {
        this._runner = runner;
        this._id = id;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        JobStatus status = this._runner.FindStatus(arguments.GetJobId(this._id)) ?? throw new JobNotFoundException();

        return ValueTask.FromResult(JsonSerializer.Serialize(value: status, jsonTypeInfo: CoreJsonContext.Default.JobStatus));
    }
}
