using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class CleanupTool : BuildTool
{
    public const int FILES_PER_STEP = 200;

    private readonly ToolContext _context;
    private readonly SchemaProperty _scope;

    public CleanupTool(ToolContext context)
        : this(context: context, scope: ScopeProperty()) { }

    private CleanupTool(ToolContext context, SchemaProperty scope)
        : base(
            name: ToolNames.CLEANUP,
            title: "Format C# files",
            description: "Starts a job that runs dotnet tool restore, then the repository's formatter, dotnet cscleanup, on the .cs files changed against the merge-base with the default branch (committed, staged, unstaged and untracked; scope changed) or on every tracked .cs file (scope all). It rewrites files in place; job_result lists in changedFiles the ones it changed. Needs a .NET tool manifest in the repository. Returns the job id at once; call job_result to wait for its outcome",
            schema: [scope],
            readOnly: false
        )
    {
        this._context = context;
        this._scope = scope;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        string scope = arguments.GetChoice(this._scope) ?? CleanupFileSelector.SCOPE_CHANGED;
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        ToolManifest.Require(repository: repository, tool: "cscleanup");

        CleanupSelection selection = await CleanupFileSelector.SelectAsync(
            repositoryRoot: repository.RepositoryRoot,
            scope: scope,
            cancellationToken: cancellationToken
        );

        if (selection.Error is not null)
        {
            throw new ToolUnavailableException(selection.Error);
        }

        if (selection.Files.Count == 0)
        {
            throw new ToolUnavailableException(
                selection.BaseReference is null
                    ? "There are no tracked .cs files to clean up"
                    : "No .cs files have changed against "
                        + selection.BaseReference
                        + ", so there is nothing to clean up"
            );
        }

        FileContentProbe probe = await FileContentProbe.SnapshotAsync(
            repositoryRoot: repository.RepositoryRoot,
            files: selection.Files,
            cancellationToken: cancellationToken
        );

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.CLEANUP,
            steps: [DotnetCommands.ToolRestore(repository), .. Steps(repository: repository, files: selection.Files)],
            testParser: null,
            coverageFiles: [],
            changedFiles: probe,
            cancellationToken: cancellationToken
        );
    }

    public static IReadOnlyList<ProcessStep> Steps(DotnetRepository repository, IReadOnlyList<string> files)
    {
        string[][] batches = [.. files.Chunk(FILES_PER_STEP)];

        if (batches.Length == 1)
        {
            return [DotnetCommands.Cleanup(repository: repository, name: "cscleanup", files: batches[0])];
        }

        return
        [
            .. batches.Select(
                (batch, index) =>
                    DotnetCommands.Cleanup(
                        repository: repository,
                        name: "cscleanup "
                            + (index + 1).ToString(CultureInfo.InvariantCulture)
                            + "/"
                            + batches.Length.ToString(CultureInfo.InvariantCulture),
                        files: batch
                    )
            ),
        ];
    }

    private static SchemaProperty ScopeProperty()
    {
        return new(
            Name: "scope",
            Description: "changed formats the .cs files changed against the default branch; all formats every tracked .cs file",
            Values: [CleanupFileSelector.SCOPE_CHANGED, CleanupFileSelector.SCOPE_ALL],
            Pattern: null,
            DefaultValue: CleanupFileSelector.SCOPE_CHANGED,
            Required: false
        );
    }
}
