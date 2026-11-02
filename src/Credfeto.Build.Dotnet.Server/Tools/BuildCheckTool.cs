using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class BuildCheckTool : BuildTool
{
    public const string MODE_PRE_RELEASE = "pre_release";
    public const string MODE_RELEASE = "release";

    private readonly ToolContext _context;
    private readonly SchemaProperty _mode;

    public BuildCheckTool(ToolContext context)
        : this(context: context, mode: ModeProperty()) { }

    private BuildCheckTool(ToolContext context, SchemaProperty mode)
        : base(
            name: ToolNames.BUILDCHECK,
            title: "Check project settings",
            description: "Starts a job that runs dotnet tool restore, then the repository's dotnet buildcheck on the solution with warnings as errors, for a pre-release or a release build. Needs a .NET tool manifest in the repository. Returns the job id at once; poll job_status, then read job_result",
            schema: [mode],
            readOnly: false
        )
    {
        this._context = context;
        this._mode = mode;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        string mode = arguments.GetChoice(this._mode) ?? MODE_PRE_RELEASE;
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        ToolManifest.Require(repository: repository, tool: "buildcheck");

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.BUILDCHECK,
            steps:
            [
                DotnetCommands.ToolRestore(repository),
                DotnetCommands.BuildCheck(repository: repository, preRelease: StringComparer.Ordinal.Equals(x: mode, y: MODE_PRE_RELEASE)),
            ],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static SchemaProperty ModeProperty()
    {
        return new(
            Name: "mode",
            Description: "pre_release checks as for a pre-release build (-PreReleaseBuild true); release as for a release build",
            Values: [MODE_PRE_RELEASE, MODE_RELEASE],
            Pattern: null,
            DefaultValue: MODE_PRE_RELEASE,
            Required: false
        );
    }
}
