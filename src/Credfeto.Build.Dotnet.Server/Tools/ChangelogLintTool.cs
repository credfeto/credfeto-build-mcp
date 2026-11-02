using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class ChangelogLintTool : BuildTool
{
    private readonly ToolContext _context;

    public ChangelogLintTool(ToolContext context)
        : base(
            name: ToolNames.CHANGELOG_LINT,
            title: "Lint the changelog",
            description: "Starts a job that runs dotnet tool restore, then dotnet changelog --changelog <repo>/CHANGELOG.md --lint, as pr-lint does. Needs a .NET tool manifest and a CHANGELOG.md at the repository root. Returns the job id at once; poll job_status, then read job_result",
            schema: [],
            readOnly: false
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        ToolManifest.Require(repository: repository, tool: "changelog");

        if (!File.Exists(DotnetCommands.ChangelogPath(repository)))
        {
            throw new ToolUnavailableException("The repository has no " + DotnetCommands.CHANGELOG_FILE + " at its root");
        }

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.CHANGELOG_LINT,
            steps: [DotnetCommands.ToolRestore(repository), DotnetCommands.ChangelogLint(repository)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }
}
