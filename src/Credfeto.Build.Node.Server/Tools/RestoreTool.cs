using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class RestoreTool : BuildTool
{
    private readonly ToolContext _context;

    public RestoreTool(ToolContext context)
        : base(
            name: ToolNames.RESTORE,
            title: "Restore npm packages",
            description: "Starts a job that runs npm ci --ignore-scripts --no-audit --no-fund in every install root, installing exactly what the lock file says. This is the only way to install; build, lint and test install first by themselves when node_modules is missing or out of date. Returns the job id at once; call job_result to wait for its outcome",
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
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string npm = this._context.Npm.Require();

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.RESTORE,
            steps:
            [
                .. repository.InstallRoots.Select(installRoot =>
                    NpmCommands.Ci(repository: repository, installRoot: installRoot, npm: npm)
                ),
            ],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
