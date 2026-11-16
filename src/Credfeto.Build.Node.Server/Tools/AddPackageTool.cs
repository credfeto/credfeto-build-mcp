using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class AddPackageTool : BuildTool
{
    private static readonly SchemaProperty Version = NpmPackageArguments.VersionProperty(
        "The version, range or dist-tag to add; the latest when left out"
    );

    private static readonly SchemaProperty Dev = new(
        Name: "dev",
        Description: "Whether it goes into devDependencies rather than dependencies",
        Values: null,
        Pattern: null,
        DefaultValue: SchemaProperty.FALSE,
        Required: false
    )
    {
        Flag = true,
    };

    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public AddPackageTool(NodeRepository repository, ToolContext context)
        : this(
            context: context,
            package: new(
                Name: PackageChoice.NAME,
                Description: "The package whose package.json gets the dependency: an install root or a workspace package",
                Values: Names(repository),
                Pattern: null,
                DefaultValue: null,
                Required: true
            )
        ) { }

    private AddPackageTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.ADD_PACKAGE,
            title: "Add an npm dependency",
            description: "Starts a job that adds a dependency to one package's package.json with npm install <package_id>@<version> --ignore-scripts (with --workspace for a workspace package), updates the lock file and installs it, then lists the version installed. Use this rather than npm install. Returns the job id at once; call job_result to wait for its outcome",
            schema: [NpmPackageArguments.PackageId, Version, Dev, package],
            readOnly: false
        )
    {
        this._context = context;
        this._package = package;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        string packageId = NpmPackageArguments.Id(arguments);
        string? version = NpmPackageArguments.Version(arguments: arguments, property: Version);
        bool dev = arguments.GetFlag(Dev);
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string name = arguments.GetChoice(property: this._package, allowed: Names(repository))
            ?? throw new ToolArgumentException("Missing required parameter " + PackageChoice.NAME);
        NodePackage package = repository.Packages.First(candidate =>
            StringComparer.Ordinal.Equals(x: candidate.Name, y: name)
        );
        string npm = this._context.Npm.Require();
        string specification = version is null ? packageId : packageId + "@" + version;

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.ADD_PACKAGE,
            steps:
            [
                NpmCommands.Install(
                    repository: repository,
                    package: package,
                    specification: specification,
                    section: dev ? DependencySection.DevDependencies : DependencySection.Dependencies,
                    npm: npm
                ),
                NpmCommands.List(repository: repository, package: package, packageId: packageId, npm: npm),
            ],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static string[] Names(NodeRepository repository)
    {
        return
        [
            .. repository
                .Packages.Select(static package => package.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }
}
