using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class RestoreTool : BuildTool
{
    private readonly ToolContext _context;

    public RestoreTool(ToolContext context)
        : base(
            name: ToolNames.RESTORE,
            title: "Restore packages",
            description: "Starts a job that runs dotnet restore on the solution. Returns the job id at once; poll job_status, then read job_result",
            schema: [],
            readOnly: false
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.RESTORE,
            steps: [DotnetCommands.Restore(repository)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
