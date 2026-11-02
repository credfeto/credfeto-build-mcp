using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
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

    private readonly SchemaProperty _project;
    private readonly DotnetRepository _repository;
    private readonly IJobRunner _runner;

    public CoverageTool(DotnetRepository repository, IJobRunner runner)
        : this(repository: repository, runner: runner, project: ProjectProperty(repository)) { }

    private CoverageTool(DotnetRepository repository, IJobRunner runner, SchemaProperty project)
        : base(
            name: ToolNames.COVERAGE,
            title: "Measure line coverage",
            description: "Starts a job that collects Cobertura coverage for one unit test project, or all of them one at a time, into coverage/<Assembly>.coverage.cobertura.xml, then reports per-assembly and Overall line coverage with the head SHA in COVERAGE.md shape. Returns the job id at once; poll job_status, then read job_result",
            schema: [project],
            readOnly: false
        )
    {
        this._repository = repository;
        this._runner = runner;
        this._project = project;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        string choice = arguments.GetChoice(this._project) ?? ALL;

        DotnetProject[] projects =
        [
            .. this._repository.UnitTestProjects.Where(project =>
                StringComparer.Ordinal.Equals(x: choice, y: ALL) || StringComparer.Ordinal.Equals(x: project.Name, y: choice)
            ),
        ];

        string[] coverageFiles = [.. projects.Select(project => DotnetCommands.CoverageFile(repository: this._repository, project: project))];
        PrepareCoverageDirectory(repository: this._repository, coverageFiles: coverageFiles);

        return JobStarter.StartAsync(
            runner: this._runner,
            repository: this._repository,
            kind: ToolNames.COVERAGE,
            steps: [.. projects.Select(project => DotnetCommands.Coverage(repository: this._repository, project: project))],
            testParser: new MtpTestOutputParser(),
            coverageFiles: coverageFiles,
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
            Values: [ALL, .. repository.UnitTestProjects.Select(static project => project.Name)],
            Pattern: null,
            DefaultValue: ALL,
            Required: false
        );
    }
}
