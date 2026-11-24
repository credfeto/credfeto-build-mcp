using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Testing;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Python.Server.Tools;

// coverage: coverage run -m pytest and coverage xml in each chosen project, read through the shared Cobertura parser
// as one row per project in the Python section of COVERAGE.md
public sealed class CoverageTool : BuildTool
{
    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public CoverageTool(PythonRepository repository, ToolContext context)
        : this(
            context: context,
            package: ProjectChoice.Property(
                repository: repository,
                description: "all, or the project whose coverage is measured"
            )
        ) { }

    private CoverageTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.COVERAGE,
            title: "Measure Python line coverage",
            description: "Starts a job that runs coverage run -m pytest (the test tool's flags) with each chosen project's .venv interpreter from the project's directory, then coverage xml to a server-owned file, installing first when the venv is missing or its manifests changed. The repository's own coverage configuration applies. Line coverage is reported per project and Overall. With package all, a successful run always writes its figures into the Python section and commit line of COVERAGE.md at the repository root, leaving every other section as it is; a single-project run replaces only that project's row and sums Overall (Python) again from every row's line counts, leaving Overall unchanged when a row has no counts. A failed run records nothing. Returns the job id at once; call job_result to wait for its outcome",
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
        string choice = arguments.GetChoice(property: this._package, allowed: ProjectChoice.Choices(repository))
            ?? PythonDetector.ALL_PROJECTS;
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
        Dictionary<string, string> components = [with(StringComparer.Ordinal)];

        foreach (InstallContext project in prepared)
        {
            components[project.Environment.CoverageReportPath] = project.Project.Name;
            DeleteStale(project.Environment);
        }

        // A run of every project always records its figures in COVERAGE.md (the repository owner's rule: there is no
        // option not to). A single-project run replaces only its project's row and sums Overall again from the rows'
        // line counts
        bool all = StringComparer.Ordinal.Equals(x: choice, y: PythonDetector.ALL_PROJECTS);

        string status = await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.COVERAGE,
            steps:
            [
                .. prepared.SelectMany(ProjectSetup.InstallIfNeeded),
                .. prepared.SelectMany(PythonCommands.Coverage),
            ],
            testParser: new PytestOutputParser(),
            coverage: new(
                Files: [.. components.Keys],
                ReportPath: Path.Combine(path1: repository.RepositoryRoot, path2: CoverageFile.FILE_NAME),
                Language: CoverageLanguage.Python,
                Components: components
            )
            {
                ReportComponent = all ? null : projects[0].Name,
            },
            changedFiles: null,
            cancellationToken: cancellationToken
        );

        return ToolNotes.Add(status: status, notes: ToolNotes.UndeclaredTools(projects));
    }

    // A report or data file from an earlier run must never be read as this run's coverage
    private static void DeleteStale(VirtualEnvironment environment)
    {
        foreach (
            string file in new[] { environment.CoverageReportPath, environment.CoverageDataPath }.Where(File.Exists)
        )
        {
            File.Delete(file);
        }
    }
}
