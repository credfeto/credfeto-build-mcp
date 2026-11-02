using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class JobResultTool : BuildTool
{
    private readonly SchemaProperty _id;
    private readonly IJobRunner _runner;

    public JobResultTool(IJobRunner runner)
        : this(runner: runner, id: JobIdProperty.Create()) { }

    private JobResultTool(IJobRunner runner, SchemaProperty id)
        : base(
            name: ToolNames.JOB_RESULT,
            title: "Job result",
            description: "Returns a job's outcome: state, per-step exit codes, test counts and failing tests, coverage figures, the masked last lines of output and the full log path. While the job runs, state is running and the figures are partial",
            schema: [id],
            readOnly: true
        )
    {
        this._runner = runner;
        this._id = id;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        JobResult result = this._runner.FindResult(arguments.GetJobId(this._id)) ?? throw new JobNotFoundException();

        return ValueTask.FromResult(JsonSerializer.Serialize(value: result, jsonTypeInfo: CoreJsonContext.Default.JobResult));
    }
}
