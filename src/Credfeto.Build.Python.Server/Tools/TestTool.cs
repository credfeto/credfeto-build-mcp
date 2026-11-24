using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Testing;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Python.Server.Tools;

public sealed class TestTool : BuildTool
{
    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public TestTool(PythonRepository repository, ToolContext context)
        : this(
            context: context,
            package: ProjectChoice.Property(repository: repository, description: "all, or the project whose tests run")
        ) { }

    private TestTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.TEST,
            title: "Run Python tests",
            description: "Starts a job that runs pytest (-p no:cacheprovider --color=no -rfE) with each chosen project's .venv interpreter, from the project's directory, so the repository's own pytest configuration applies. It installs first, as restore does, when the venv is missing or its manifests changed. Returns the job id at once; call job_result to wait for its outcome, with pass/fail/skip counts (errors count as failures) and the failing test ids",
            schema: [package],
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
        PythonRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        IReadOnlyList<PythonProject> projects = ProjectChoice.Select(
            arguments: arguments,
            property: this._package,
            repository: repository
        );
        IReadOnlyList<InstallContext> prepared = await ProjectSetup.PrepareAsync(
            context: this._context,
            repository: repository,
            projects: projects,
            cancellationToken: cancellationToken
        );

        string status = await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.TEST,
            steps: [.. prepared.SelectMany(ProjectSetup.InstallIfNeeded), .. prepared.Select(PythonCommands.Pytest)],
            testParser: new PytestOutputParser(),
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );

        return ToolNotes.Add(status: status, notes: ToolNotes.UndeclaredTools(projects));
    }
}
