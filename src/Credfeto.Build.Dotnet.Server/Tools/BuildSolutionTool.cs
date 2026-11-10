using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class BuildSolutionTool : BuildTool
{
    private readonly ToolContext _context;

    public BuildSolutionTool(ToolContext context)
        : base(
            name: ToolNames.BUILD,
            title: "Build (Release)",
            description: "Starts a job that restores, then builds the whole solution as pre-commit buildtest does: dotnet build <solution> -c Release --no-restore with buildtest's -p: properties and -nodeReuse:False, from the solution directory. Returns the job id at once; poll job_status, then read job_result",
            schema: [],
            readOnly: false
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.BUILD,
            steps: [DotnetCommands.Restore(repository), DotnetCommands.Build(repository)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
