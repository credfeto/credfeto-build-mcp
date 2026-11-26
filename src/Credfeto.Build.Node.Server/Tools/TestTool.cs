using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Testing;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class TestTool : BuildTool
{
    public const string NO_UNIT_SCRIPT =
        "The package has no unit test script: neither it nor its install root's package has test:noe2e, and unit never runs a workspace package's test script. Use scope all to run its test script";

    private static readonly SchemaProperty Scope = new(
        Name: "scope",
        Description: "unit runs test:noe2e (as the pre-commit hook does): the package's own, otherwise its install root's test:noe2e limited to the package's directory; an install root's own package without test:noe2e runs test. all runs test",
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
            description: "Starts a job that runs the test script of the package chosen in package with CI=true, installing first when node_modules is missing or older than the lock file. scope unit (the default) runs test:noe2e, as the pre-commit hook does: a workspace package without its own runs its install root's test:noe2e from the install root with the package's directory after --, and is refused with no job started when the install root's package has none either; only an install root's own package falls back to test. all prefers test; node.testScript in .ai/commands.json replaces the choice for packages that have it. all runs the script once at each install root whose own package has it, otherwise in each workspace package that has it. Returns the job id at once; call job_result to wait for its outcome, with pass/fail counts and failing test names for Vitest, Jest and node --test",
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
        string choice = PackageChoice.Choice(
            arguments: arguments,
            property: this._package,
            repository: repository,
            script: package => AnyTestScript(repository: repository, package: package)
        );
        IReadOnlyList<ScriptRun> runs = Runs(repository: repository, choice: choice, scope: scope);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.TEST,
            steps: JobSteps.ForRuns(repository: repository, runs: runs, npm: npm),
            testParser: CompositeTestOutputParser.For(runs.Select(static run => run.Package.TestRunner)),
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static IReadOnlyList<ScriptRun> Runs(NodeRepository repository, string choice, string scope)
    {
        ScriptRun? Run(NodePackage package) =>
            TestScripts.For(
                repository: repository,
                package: package,
                scope: scope,
                overrideScript: repository.TestScript
            );

        IReadOnlyList<ScriptRun> runs =
        [
            .. ScriptTargets
                .Packages(repository: repository, choice: choice, hasScript: package => Run(package) is not null)
                .Select(Run)
                .OfType<ScriptRun>(),
        ];

        return runs.Count != 0
            ? runs
            : throw new ToolUnavailableException(NoRunsReason(repository: repository, choice: choice, scope: scope));
    }

    // A named package that has a test script but none unit can run is told why, rather than that the script is gone
    private static string NoRunsReason(NodeRepository repository, string choice, string scope)
    {
        return
            repository.TestScript is null
            && StringComparer.Ordinal.Equals(x: scope, y: TestScripts.SCOPE_UNIT)
            && !StringComparer.Ordinal.Equals(x: choice, y: ScriptTargets.ALL)
            ? NO_UNIT_SCRIPT
            : PackageChoice.NO_SCRIPT;
    }

    // Whether the package has a test script under either scope, so the package enum does not depend on the scope
    private static string? AnyTestScript(NodeRepository repository, NodePackage package)
    {
        return TestScripts
            .For(
                repository: repository,
                package: package,
                scope: TestScripts.SCOPE_ALL,
                overrideScript: repository.TestScript
            )
            ?.Script;
    }
}
