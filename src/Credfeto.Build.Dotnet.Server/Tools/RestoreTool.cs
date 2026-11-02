using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class RestoreTool : BuildTool
{
    private readonly DotnetRepository _repository;
    private readonly IJobRunner _runner;

    public RestoreTool(DotnetRepository repository, IJobRunner runner)
        : base(
            name: ToolNames.RESTORE,
            title: "Restore packages",
            description: "Starts a job that runs dotnet restore on the solution. Returns the job id at once; poll job_status, then read job_result",
            schema: [],
            readOnly: false
        )
    {
        this._repository = repository;
        this._runner = runner;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        return JobStarter.StartAsync(
            runner: this._runner,
            repository: this._repository,
            kind: ToolNames.RESTORE,
            steps: [DotnetCommands.Restore(this._repository)],
            testParser: null,
            coverageFiles: [],
            cancellationToken: cancellationToken
        );
    }
}
