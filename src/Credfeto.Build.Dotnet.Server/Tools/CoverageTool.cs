using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Testing;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>
///     Runs the documented coverage form per unit test project, then combines the Cobertura files in-process.
/// </summary>
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
            description: "Starts a job that collects Cobertura coverage for one unit test project, or all of them one at a time, into coverage/<Assembly>.coverage.cobertura.xml, then reports per-assembly and Overall line coverage with the head SHA in COVERAGE.md shape. Returns the job id at once; poll job_status, then read job_result",
            schema: [project],
            readOnly: false
        )
    {
        this._context = context;
        this._project = project;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string choice = arguments.GetChoice(property: this._project, allowed: [ALL, .. UnitTestProjectChoice.Names(repository)]) ?? ALL;
        IReadOnlyList<DotnetProject> projects = UnitTestProjectChoice.Select(repository: repository, choice: choice, all: ALL);

        string[] coverageFiles = [.. projects.Select(project => DotnetCommands.CoverageFile(repository: repository, project: project))];
        PrepareCoverageDirectory(repository: repository, coverageFiles: coverageFiles);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.COVERAGE,
            steps: [.. projects.Select(project => DotnetCommands.Coverage(repository: repository, project: project))],
            testParser: new MtpTestOutputParser(),
            coverageFiles: coverageFiles,
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
