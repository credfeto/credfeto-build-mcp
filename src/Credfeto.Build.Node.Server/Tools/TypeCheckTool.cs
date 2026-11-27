using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

// A compile check that needs no build arguments: the package's typecheck script, otherwise the locally installed
// tsc --noEmit against its tsconfig.json; offered only when some package has one or the other
public sealed class TypeCheckTool : BuildTool
{
    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public TypeCheckTool(NodeRepository repository, ToolContext context)
        : this(
            context: context,
            package: PackageChoice.Property(
                choices: ScriptTargets.Choices(repository: repository, script: Check),
                description: "all, or the package whose types are checked"
            )
        ) { }

    private TypeCheckTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.TYPECHECK,
            title: "Type check npm packages",
            description: "Starts a job that checks that the package chosen in package compiles, installing first when node_modules is missing or older than the lock file. It runs npm run typecheck when the package has a typecheck script (a workspace package from its install root with --workspace), otherwise tsc --noEmit -p tsconfig.json from the package directory with the tsc installed in the package's or its install root's node_modules; tsc is never downloaded. all runs once at each install root whose own package qualifies, otherwise in each workspace package that does. Returns the job id at once; call job_result to wait for its outcome",
            schema: [package],
            readOnly: false
        )
    {
        this._context = context;
        this._package = package;
    }

    public static bool Offered(NodeRepository repository)
    {
        return repository.Packages.Any(static package => Check(package) is not null);
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string npm = this._context.Npm.Require();

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.TYPECHECK,
            steps: JobSteps.ForTypeChecks(
                repository: repository,
                targets: PackageChoice.Targets(
                    arguments: arguments,
                    property: this._package,
                    repository: repository,
                    script: Check
                ),
                npm: npm
            ),
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    // The package's own typecheck script wins over the tsc fallback, as it knows how the package is meant to be checked
    private static string? Check(NodePackage package)
    {
        if (package.HasScript(NodeScripts.TYPECHECK))
        {
            return NodeScripts.TYPECHECK;
        }

        return package.HasTsConfig ? NpmCommands.TSC : null;
    }
}
