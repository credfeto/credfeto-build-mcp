using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Testing;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class TestTool : BuildTool
{
    public const string SCOPE_ALL = "all";
    public const string SCOPE_PROJECT = "project";

    private readonly ToolContext _context;
    private readonly SchemaProperty _project;
    private readonly SchemaProperty _scope;

    public TestTool(DotnetRepository repository, ToolContext context)
        : this(context: context, scope: ScopeProperty(), project: ProjectProperty(repository)) { }

    private TestTool(ToolContext context, SchemaProperty scope, SchemaProperty project)
        : base(
            name: ToolNames.TEST,
            title: "Run tests",
            description: "Starts a job that restores, builds and then runs the tests in Release with the settings pre-commit buildtest uses. scope all builds the solution, then runs one solution-wide dotnet test --solution <solution> --no-build that excludes the *.Integration.Tests and *.Benchmark.Tests namespaces, as buildtest does; scope project builds and runs only the test project named in project, which may be a unit, integration or benchmark test project, with no namespace filter. Returns the job id at once; poll job_status, then read job_result for pass/fail counts and failing test names",
            schema: [scope, project],
            readOnly: false
        )
    {
        this._context = context;
        this._scope = scope;
        this._project = project;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        string scope = arguments.GetChoice(this._scope) ?? SCOPE_ALL;
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string? project = arguments.GetChoice(property: this._project, allowed: ProjectNames(repository));

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.TEST,
            steps: Steps(repository: repository, scope: scope, project: project),
            testParser: new MtpTestOutputParser(),
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    // Restore and build stop the job when they fail, so the --no-build test step never runs stale binaries
    private static IReadOnlyList<ProcessStep> Steps(DotnetRepository repository, string scope, string? project)
    {
        if (StringComparer.Ordinal.Equals(x: scope, y: SCOPE_ALL))
        {
            return project is null
                ?
                [
                    DotnetCommands.Restore(repository),
                    DotnetCommands.Build(repository),
                    DotnetCommands.TestSolution(repository),
                ]
                : throw new ToolArgumentException("project is only allowed with scope project");
        }

        if (project is null)
        {
            throw new ToolArgumentException("scope project needs a project");
        }

        DotnetProject chosen = TestProjects(repository)
            .First(candidate => StringComparer.Ordinal.Equals(x: candidate.Name, y: project));

        return
        [
            DotnetCommands.Restore(repository),
            DotnetCommands.BuildProject(repository: repository, project: chosen),
            DotnetCommands.Test(repository: repository, project: chosen),
        ];
    }

    // Integration and benchmark test projects are left out of scope all, so they can be asked for by name
    private static IEnumerable<DotnetProject> TestProjects(DotnetRepository repository)
    {
        return repository.UnitTestProjects.Concat(
            repository.Projects.Where(static project =>
                project.Kind is DotnetProjectKind.IntegrationTests or DotnetProjectKind.BenchmarkTests
            )
        );
    }

    private static string[] ProjectNames(DotnetRepository repository)
    {
        return [.. TestProjects(repository).Select(static project => project.Name)];
    }

    private static SchemaProperty ScopeProperty()
    {
        return new(
            Name: "scope",
            Description: "all runs the solution's tests in one run, without the integration and benchmark test namespaces; project runs only the one named in project",
            Values: [SCOPE_ALL, SCOPE_PROJECT],
            Pattern: null,
            DefaultValue: SCOPE_ALL,
            Required: false
        );
    }

    private static SchemaProperty ProjectProperty(DotnetRepository repository)
    {
        return new(
            Name: "project",
            Description: "The unit, integration or benchmark test project to run; only with scope project",
            Values: ProjectNames(repository),
            Pattern: null,
            DefaultValue: null,
            Required: false
        );
    }
}
