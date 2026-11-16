using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class BuildtestTool : BuildTool
{
    private readonly ToolContext _context;

    public BuildtestTool(ToolContext context)
        : base(
            name: ToolNames.BUILDTEST,
            title: "Run everything pre-commit checks",
            description: "Starts a job that runs the installed pre-commit buildtest script from the repository root, with no arguments: tool restore, code analysis, restore, clean, Release build, API compatibility check, tests, publish and pack. Use before committing; build, test and the quality tools are quicker for iteration. Returns the job id at once; call job_result to wait for its outcome",
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
        string script = this._context.Buildtest.Require();
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.BUILDTEST,
            steps: [DotnetCommands.Buildtest(repository: repository, script: script)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
