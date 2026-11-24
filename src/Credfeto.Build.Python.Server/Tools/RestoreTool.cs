using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Tools;

public sealed class RestoreTool : BuildTool
{
    private const string DESCRIPTION =
        "Starts a job that installs each chosen project's dependencies into its own <project>/.venv: pip (a venv from python3, then pip install --only-binary :all: of the requirements files, [project].dependencies and the dev/test/tests extras and groups), or uv sync --frozen --no-build for a project with uv.lock. No package code runs at install time and the project itself is not built, so a package with no wheel for this Python fails the install. pytest and coverage are installed unpinned when the project does not declare them, and the result's notes say so. A repository with Python tests but no manifest gets a minimal pyproject.toml (or requirements-dev.txt) created first, which the notes ask you to commit. This is the only way to install; test and lint install first by themselves when the manifests changed. Returns the job id at once; call job_result to wait for its outcome";

    private readonly ToolContext _context;
    private readonly SchemaProperty? _package;

    public RestoreTool(PythonRepository? repository, ToolContext context)
        : this(
            context: context,
            package: repository is null
                ? null
                : ProjectChoice.Property(repository: repository, description: "all, or the project to install")
        ) { }

    private RestoreTool(ToolContext context, SchemaProperty? package)
        : base(
            name: ToolNames.RESTORE,
            title: "Restore Python dependencies",
            description: DESCRIPTION,
            schema: package is null ? [] : [package],
            readOnly: false
        )
    {
        this._context = context;
        this._package = package;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        PythonDetection detection = await this._context.Detection.RefreshAsync(cancellationToken);
        List<string> notes = [];

        if (detection.Repository is null && detection.TestSuite is { } testSuite)
        {
            string created = await ManifestCreator.CreateAsync(
                repositoryRoot: detection.RepositoryRoot,
                testSuite: testSuite,
                cancellationToken: cancellationToken
            );
            notes.Add(ManifestCreator.Note(created));
            detection = await this._context.Detection.RefreshAsync(cancellationToken);
        }

        PythonRepository repository = ToolContext.RepositoryOf(detection);
        IReadOnlyList<PythonProject> projects = this._package is null
            ? Supported(repository)
            : ProjectChoice.Select(arguments: arguments, property: this._package, repository: repository);
        IReadOnlyList<InstallContext> prepared = await ProjectSetup.PrepareAsync(
            context: this._context,
            repository: repository,
            projects: projects,
            cancellationToken: cancellationToken
        );
        notes.AddRange(ToolNotes.UndeclaredTools(projects));

        string status = await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.RESTORE,
            steps: [.. prepared.SelectMany(PythonCommands.Install)],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );

        return ToolNotes.Add(status: status, notes: notes);
    }

    private static IReadOnlyList<PythonProject> Supported(PythonRepository repository)
    {
        return repository.SupportedProjects.Count != 0
            ? repository.SupportedProjects
            : throw new ToolUnavailableException("There is no supported Python project");
    }
}
