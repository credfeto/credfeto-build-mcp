using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Testing;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class CoverageTool : BuildTool
{
    public const string ALL = "all";

    private readonly ToolContext _context;
    private readonly SchemaProperty _project;

    public CoverageTool(DotnetRepository repository, ToolContext context)
        : this(context: context, project: ProjectProperty(repository)) { }

    private CoverageTool(ToolContext context, SchemaProperty project)
        : base(
            name: ToolNames.COVERAGE,
            title: "Measure line coverage",
            description: "Starts a job that collects Cobertura coverage for one unit test project, or all of them one at a time, into coverage/<Assembly>.coverage.cobertura.xml, then reports per-assembly and Overall line coverage with the head SHA in COVERAGE.md shape. A successful run of all projects always writes its figures into the .NET section and commit line of COVERAGE.md at the repository root, leaving every other section as it is; a single-project run replaces only its assembly's row and sums Overall (.NET) again from every row's line counts, leaving Overall unchanged when a row has no counts. Returns the job id at once; call job_result to wait for its outcome",
            schema: [project],
            readOnly: false
        )
    {
        this._context = context;
        this._project = project;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string choice = arguments.GetChoice(
            property: this._project,
            allowed: [ALL, .. UnitTestProjectChoice.Names(repository)]
        ) ?? ALL;

        IReadOnlyList<DotnetProject> projects = UnitTestProjectChoice.Select(
            repository: repository,
            choice: choice,
            all: ALL
        );

        string[] coverageFiles =
        [
            .. projects.Select(project => DotnetCommands.CoverageFile(repository: repository, project: project)),
        ];
        PrepareCoverageDirectory(repository: repository, coverageFiles: coverageFiles);

        // A full run always records its figures in COVERAGE.md (the repository owner's rule: there is no option not to).
        // A single-project run replaces only its assembly's row and sums Overall again from the rows' line counts
        bool all = StringComparer.Ordinal.Equals(x: choice, y: ALL);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.COVERAGE,
            steps: [.. projects.Select(project => DotnetCommands.Coverage(repository: repository, project: project))],
            testParser: new MtpTestOutputParser(),
            coverage: new(
                Files: coverageFiles,
                ReportPath: projects.Count != 0
                    ? Path.Combine(path1: repository.RepositoryRoot, path2: CoverageFile.FILE_NAME)
                    : null,
                Language: CoverageLanguage.Dotnet,
                Components: null
            )
            {
                ReportComponent = all || projects.Count == 0 ? null : DotnetCommands.AssemblyUnderTest(projects[0]),
            },
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static void PrepareCoverageDirectory(DotnetRepository repository, IReadOnlyList<string> coverageFiles)
    {
        _ = Directory.CreateDirectory(DotnetCommands.CoverageDirectory(repository));

        // A stale file from an earlier run must never be reported as this run's coverage
        foreach (string file in coverageFiles)
        {
            File.Delete(file);
        }
    }

    private static SchemaProperty ProjectProperty(DotnetRepository repository)
    {
        return new(
            Name: "project",
            Description: "The unit test project to measure, or all to measure each one in turn",
            Values: [ALL, .. UnitTestProjectChoice.Names(repository)],
            Pattern: null,
            DefaultValue: ALL,
            Required: false
        );
    }
}
