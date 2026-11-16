using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Testing;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class TestTool : BuildTool
{
    private static readonly SchemaProperty Scope = new(
        Name: "scope",
        Description: "unit runs test:noe2e where a package has it (as the pre-commit hook does), otherwise test; all runs test",
        Values: [TestScripts.SCOPE_UNIT, TestScripts.SCOPE_ALL],
        Pattern: null,
        DefaultValue: TestScripts.SCOPE_UNIT,
        Required: false
    );

    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public TestTool(NodeRepository repository, ToolContext context)
        : this(
            context: context,
            package: PackageChoice.Property(
                choices: ScriptTargets.Choices(repository: repository, script: package =>
                    AnyTestScript(repository: repository, package: package)
                ),
                description: "all, or the package whose tests run"
            )
        ) { }

    private TestTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.TEST,
            title: "Run npm tests",
            description: "Starts a job that runs the test script of the package chosen in package with CI=true, installing first when node_modules is missing or older than the lock file. scope unit (the default) prefers test:noe2e, as the pre-commit hook does; all prefers test; node.testScript in .ai/commands.json replaces the choice for packages that have it. all runs the script once at each install root whose own package has it, otherwise in each workspace package that has it. Returns the job id at once; call job_result to wait for its outcome, with pass/fail counts and failing test names for Vitest, Jest and node --test",
            schema: [Scope, package],
            readOnly: false
        )
    {
        this._context = context;
        this._package = package;
    }

    public static bool Offered(NodeRepository repository)
    {
        return repository.Packages.Any(package => AnyTestScript(repository: repository, package: package) is not null);
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        string scope = arguments.GetChoice(Scope) ?? TestScripts.SCOPE_UNIT;
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string npm = this._context.Npm.Require();
        IReadOnlyList<(NodePackage Package, string Script)> targets = PackageChoice.Targets(
            arguments: arguments,
            property: this._package,
            repository: repository,
            script: package => TestScripts.For(package: package, scope: scope, overrideScript: repository.TestScript)
        );

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.TEST,
            steps: JobSteps.ForScripts(repository: repository, targets: targets, npm: npm),
            testParser: CompositeTestOutputParser.For(targets.Select(static target => target.Package.TestRunner)),
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    // Whether the package has a test script under either scope, so the package enum does not depend on the scope
    private static string? AnyTestScript(NodeRepository repository, NodePackage package)
    {
        return TestScripts.For(package: package, scope: TestScripts.SCOPE_ALL, overrideScript: repository.TestScript);
    }
}
