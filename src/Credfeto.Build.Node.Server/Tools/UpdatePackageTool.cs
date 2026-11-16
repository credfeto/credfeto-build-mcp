using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class UpdatePackageTool : BuildTool
{
    private const string LATEST = "latest";

    private static readonly SchemaProperty Version = NpmPackageArguments.VersionProperty(
        "The version, range or dist-tag to update to; latest when left out"
    );

    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public UpdatePackageTool(NodeRepository repository, ToolContext context)
        : this(
            context: context,
            package: PackageChoice.Property(
                choices: Choices(repository),
                description: "all updates every package whose package.json declares the dependency; or one such package"
            )
        ) { }

    private UpdatePackageTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.UPDATE_PACKAGE,
            title: "Update an npm dependency",
            description: "Starts a job that updates a dependency in the package.json of each package chosen in package that declares it, keeping it in its dependencies, devDependencies or optionalDependencies section, with npm install <package_id>@<version> --ignore-scripts (with --workspace for a workspace package); the lock file is updated, then the version installed is listed. Use this rather than npm install or npm update. Returns the job id at once; call job_result to wait for its outcome",
            schema: [NpmPackageArguments.PackageId, Version, package],
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
        string version = NpmPackageArguments.Version(arguments: arguments, property: Version) ?? LATEST;
        NodeRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string choice = arguments.GetChoice(property: this._package, allowed: Choices(repository)) ?? ScriptTargets.ALL;
        NodePackage[] declaring =
        [
            .. repository.Packages.Where(package =>
                package.FindDependency(packageId) is not null
                && (
                    StringComparer.Ordinal.Equals(x: choice, y: ScriptTargets.ALL)
                    || StringComparer.Ordinal.Equals(x: package.Name, y: choice)
                )
            ),
        ];

        if (declaring.Length == 0)
        {
            throw new ToolArgumentException(
                "No chosen package declares package_id in its package.json; use add_package to add it"
            );
        }

        string npm = this._context.Npm.Require();

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.UPDATE_PACKAGE,
            steps: Steps(
                repository: repository,
                declaring: declaring,
                packageId: packageId,
                specification: packageId + "@" + version,
                npm: npm
            ),
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private static IReadOnlyList<ProcessStep> Steps(
        NodeRepository repository,
        IReadOnlyList<NodePackage> declaring,
        string packageId,
        string specification,
        string npm
    )
    {
        List<ProcessStep> steps = [];

        foreach (NodePackage package in declaring)
        {
            DependencySection section = package.FindDependency(packageId)?.Section ?? DependencySection.Dependencies;
            steps.Add(
                NpmCommands.Install(
                    repository: repository,
                    package: package,
                    specification: specification,
                    section: section,
                    npm: npm
                )
            );
        }

        steps.AddRange(
            declaring.Select(package =>
                NpmCommands.List(repository: repository, package: package, packageId: packageId, npm: npm)
            )
        );

        return steps;
    }

    private static string[] Choices(NodeRepository repository)
    {
        return
        [
            ScriptTargets.ALL,
            .. repository
                .Packages.Select(static package => package.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }
}
