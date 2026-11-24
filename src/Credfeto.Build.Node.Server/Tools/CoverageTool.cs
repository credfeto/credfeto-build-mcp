using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Testing;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

// coverage: runs each chosen package's own coverage script (see CoverageScripts for why only that) and reads the
// Cobertura report it writes. Offered only when some package has such a script
public sealed class CoverageTool : BuildTool
{
    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public CoverageTool(NodeRepository repository, ToolContext context)
        : this(
            context: context,
            package: PackageChoice.Property(
                choices: ScriptTargets.Choices(repository: repository, script: package =>
                    CoverageScripts.For(package: package, overrideScript: repository.CoverageScript)
                ),
                description: "all, or the package whose coverage is measured"
            )
        ) { }

    private CoverageTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.COVERAGE,
            title: "Measure npm line coverage",
            description: "Starts a job that runs the coverage script of the package chosen in package (test:coverage, or node.coverageScript in .ai/commands.json) with CI=true, installing first when node_modules is missing or older than the lock file, then reads the Cobertura report the script writes ("
                + CoverageScripts.DEFAULT_FILE
                + " in the package's directory, or node.coverageFile) and reports line coverage per package and Overall. The script must enable Vitest's cobertura reporter; nothing is added to it. With package all, a successful run always writes its figures into the Node section and commit line of COVERAGE.md at the repository root, leaving every other section as it is; a single-package run replaces only that package's row and sums Overall (Node) again from every row's line counts, leaving Overall unchanged when a row has no counts. Returns the job id at once; call job_result to wait for its outcome",
            schema: [package],
            readOnly: false
        )
    {
        this._context = context;
        this._package = package;
    }

    public static bool Offered(NodeRepository repository)
    {
        return repository.Packages.Any(package =>
            CoverageScripts.For(package: package, overrideScript: repository.CoverageScript) is not null
        );
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string npm = this._context.Npm.Require();
        string? Script(NodePackage package) =>
            CoverageScripts.For(package: package, overrideScript: repository.CoverageScript);
        string choice = arguments.GetChoice(
            property: this._package,
            allowed: ScriptTargets.Choices(repository: repository, script: Script)
        ) ?? ScriptTargets.ALL;

        IReadOnlyList<(NodePackage Package, string Script)> targets = PackageChoice.Targets(
            arguments: arguments,
            property: this._package,
            repository: repository,
            script: Script
        );
        Dictionary<string, string> components = Components(repository: repository, targets: targets);
        DeleteStaleReports(components.Keys);

        // A run of every package always records its figures in COVERAGE.md (the repository owner's rule: there is no
        // option not to). A single-package run replaces only its package's row and sums Overall again from the rows'
        // line counts
        bool all = StringComparer.Ordinal.Equals(x: choice, y: ScriptTargets.ALL);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.COVERAGE,
            steps: JobSteps.ForScripts(repository: repository, targets: targets, npm: npm),
            testParser: CompositeTestOutputParser.For(targets.Select(static target => target.Package.TestRunner)),
            coverage: new(
                Files: [.. components.Keys],
                ReportPath: Path.Combine(path1: repository.RepositoryRoot, path2: CoverageFile.FILE_NAME),
                Language: CoverageLanguage.Node,
                Components: components
            )
            {
                ReportComponent = all || targets.Count == 0 ? null : targets[0].Package.Name,
            },
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    // Each package's report is one row of COVERAGE.md, named after the package
    private static Dictionary<string, string> Components(
        NodeRepository repository,
        IReadOnlyList<(NodePackage Package, string Script)> targets
    )
    {
        Dictionary<string, string> components = [with(StringComparer.Ordinal)];

        foreach ((NodePackage package, _) in targets)
        {
            components[CoverageScripts.ReportPath(repository: repository, package: package)] = package.Name;
        }

        return components;
    }

    // A stale report from an earlier run must never be read as this run's coverage
    private static void DeleteStaleReports(IEnumerable<string> files)
    {
        // File.Delete throws when the directory is missing, which it is before a package's first run
        foreach (string file in files.Where(File.Exists))
        {
            File.Delete(file);
        }
    }
}
