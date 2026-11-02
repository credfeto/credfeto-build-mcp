using System;
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
            description: "Starts a job that runs the tests in Release. scope all is one solution-wide run, dotnet test --solution <solution> -c Release from the solution directory, which includes integration tests as CI does; scope project runs only the unit test project named in project. Returns the job id at once; poll job_status, then read job_result for pass/fail counts and failing test names",
            schema: [scope, project],
            readOnly: false
        )
    {
        this._context = context;
        this._scope = scope;
        this._project = project;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        string scope = arguments.GetChoice(this._scope) ?? SCOPE_ALL;
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string? project = arguments.GetChoice(property: this._project, allowed: UnitTestProjectChoice.Names(repository));

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repository: repository,
            kind: ToolNames.TEST,
            steps: [Step(repository: repository, scope: scope, project: project)],
            testParser: new MtpTestOutputParser(),
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static ProcessStep Step(DotnetRepository repository, string scope, string? project)
    {
        if (StringComparer.Ordinal.Equals(x: scope, y: SCOPE_ALL))
        {
            return project is null
                ? DotnetCommands.TestSolution(repository)
                : throw new ToolArgumentException("project is only allowed with scope project");
        }

        return project is null
            ? throw new ToolArgumentException("scope project needs a project")
            : DotnetCommands.Test(repository: repository, project: UnitTestProjectChoice.Find(repository: repository, name: project));
    }

    private static SchemaProperty ScopeProperty()
    {
        return new(
            Name: "scope",
            Description: "all runs every test project in the solution in one run; project runs only the one named in project",
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
            Values: UnitTestProjectChoice.Names(repository),
            Pattern: null,
            DefaultValue: null,
            Required: false
        );
    }
}
