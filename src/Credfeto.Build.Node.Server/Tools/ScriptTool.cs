using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

// build and lint: npm run of one script, for the packages that have it; offered only when some package does
public sealed class ScriptTool : BuildTool
{
    private readonly ToolContext _context;
    private readonly SchemaProperty _package;
    private readonly string _script;

    private ScriptTool(ToolContext context, string script, string title, SchemaProperty package)
        : base(
            name: script,
            title: title,
            description: "Starts a job that runs npm run "
                + script
                + " for the package chosen in package, installing first when node_modules is missing or older than the lock file. A workspace package runs from its install root with --workspace. all runs the script once at each install root whose own package has it, otherwise in each workspace package that has it. args are handed to every script run after --. Returns the job id at once; call job_result to wait for its outcome",
            schema: [package, ScriptArguments.Property],
            readOnly: false
        )
    {
        this._context = context;
        this._script = script;
        this._package = package;
    }

    public static ScriptTool Build(NodeRepository repository, ToolContext context)
    {
        return Create(repository: repository, context: context, script: NodeScripts.BUILD, title: "Build npm packages");
    }

    public static ScriptTool Lint(NodeRepository repository, ToolContext context)
    {
        return Create(repository: repository, context: context, script: NodeScripts.LINT, title: "Lint npm packages");
    }

    public static bool Offered(NodeRepository repository, string script)
    {
        return repository.Packages.Any(package => package.HasScript(script));
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> scriptArguments = arguments.GetStrings(
            property: ScriptArguments.Property,
            pattern: ScriptArguments.Argument()
        );
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string npm = this._context.Npm.Require();
        string script = this._script;
        IReadOnlyList<ScriptRun> runs =
        [
            .. PackageChoice
                .Targets(arguments: arguments, property: this._package, repository: repository, script: package =>
                    package.HasScript(script) ? script : null
                )
                .Select(target => new ScriptRun(Package: target.Package, Script: target.Script)
                {
                    RunnerArguments = scriptArguments,
                }),
        ];

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: script,
            steps: JobSteps.ForRuns(repository: repository, runs: runs, npm: npm),
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static ScriptTool Create(NodeRepository repository, ToolContext context, string script, string title)
    {
        return new(
            context: context,
            script: script,
            title: title,
            package: PackageChoice.Property(
                choices: ScriptTargets.Choices(repository: repository, script: package =>
                    package.HasScript(script) ? script : null
                ),
                description: "all, or the package whose " + script + " script runs"
            )
        );
    }
}
