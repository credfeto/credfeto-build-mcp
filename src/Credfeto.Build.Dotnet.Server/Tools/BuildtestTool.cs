using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>
///     Runs the installed pre-commit buildtest script itself, so the tool always does what pre-commit checks.
/// </summary>
public sealed class BuildtestTool : BuildTool
{
    private readonly ToolContext _context;

    public BuildtestTool(ToolContext context)
        : base(
            name: ToolNames.BUILDTEST,
            title: "Run everything pre-commit checks",
            description: "Starts a job that runs the installed pre-commit buildtest script from the repository root, with no arguments: tool restore, code analysis, restore, clean, Release build, API compatibility check, tests, publish and pack. Use before committing; build, test and the quality tools are quicker for iteration. Returns the job id at once; poll job_status, then read job_result",
            schema: [],
            readOnly: false
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        string script = this._context.Buildtest.Require();
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.BUILDTEST,
            steps: [DotnetCommands.Buildtest(repository: repository, script: script)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
