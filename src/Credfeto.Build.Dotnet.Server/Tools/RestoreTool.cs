using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class RestoreTool : BuildTool
{
    private readonly ToolContext _context;

    public RestoreTool(ToolContext context)
        : base(
            name: ToolNames.RESTORE,
            title: "Restore packages",
            description: "Starts a job that runs dotnet restore on the solution. Returns the job id at once; call job_result to wait for its outcome",
            schema: [],
            readOnly: false
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.RESTORE,
            steps: [DotnetCommands.Restore(repository)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
