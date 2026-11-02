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

    private readonly SchemaProperty _project;
    private readonly DotnetRepository _repository;
    private readonly IJobRunner _runner;
    private readonly SchemaProperty _scope;

    public TestTool(DotnetRepository repository, IJobRunner runner)
        : this(repository: repository, runner: runner, scope: ScopeProperty(), project: ProjectProperty(repository)) { }

    private TestTool(DotnetRepository repository, IJobRunner runner, SchemaProperty scope, SchemaProperty project)
        : base(
            name: ToolNames.TEST,
            title: "Run unit tests",
            description: "Starts a job that runs dotnet test -c Release for every unit test project (scope all) or one of them (scope project), one project at a time from the solution directory. Returns the job id at once; poll job_status, then read job_result for pass/fail counts and failing test names",
            schema: [scope, project],
            readOnly: false
        )
    {
        this._repository = repository;
        this._runner = runner;
        this._scope = scope;
        this._project = project;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        string scope = arguments.GetChoice(this._scope) ?? SCOPE_ALL;
        string? project = arguments.GetChoice(this._project);

        IReadOnlyList<DotnetProject> projects = Select(repository: this._repository, scope: scope, project: project);

        return JobStarter.StartAsync(
            runner: this._runner,
            repository: this._repository,
            kind: ToolNames.TEST,
            steps: [.. projects.Select(selected => DotnetCommands.Test(repository: this._repository, project: selected))],
            testParser: new MtpTestOutputParser(),
            coverageFiles: [],
            cancellationToken: cancellationToken
        );
    }

    private static IReadOnlyList<DotnetProject> Select(DotnetRepository repository, string scope, string? project)
    {
        if (StringComparer.Ordinal.Equals(x: scope, y: SCOPE_ALL))
        {
            return project is null
                ? repository.UnitTestProjects
                : throw new ToolArgumentException("project is only allowed with scope project");
        }

        return project is null
            ? throw new ToolArgumentException("scope project needs a project")
            : [repository.UnitTestProjects.First(candidate => StringComparer.Ordinal.Equals(x: candidate.Name, y: project))];
    }

    private static SchemaProperty ScopeProperty()
    {
        return new(
            Name: "scope",
            Description: "all runs every unit test project; project runs only the one named in project",
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
            Description: "The unit test project to run; only with scope project",
            Values: [.. repository.UnitTestProjects.Select(static project => project.Name)],
            Pattern: null,
            DefaultValue: null,
            Required: false
        );
    }
}
