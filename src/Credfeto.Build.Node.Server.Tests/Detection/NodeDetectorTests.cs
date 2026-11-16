using System;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Detection;

public sealed class NodeDetectorTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public async Task ARootPackageWithItsLockFileIsDetectedAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());

        NodeRepository repository = await this.DetectRepositoryAsync();

        NodeInstallRoot root = Assert.Single(repository.InstallRoots);
        Assert.Equal(expected: ".", actual: root.RelativeDirectory);
        Assert.Equal(expected: "package-lock.json", actual: root.LockFile);
        NodePackage package = Assert.Single(repository.Packages);
        Assert.Equal(expected: "example", actual: package.Name);
        Assert.Equal(expected: ".", actual: package.RelativeDirectory);
        Assert.Equal(expected: ["build", "test"], actual: package.Scripts, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: TimeSpan.FromMinutes(30), actual: repository.Timeout);
    }

    [Fact]
    public async Task APackageInSrcIsDetectedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "src",
            manifest: NpmRepository.SAMPLE_MANIFEST,
            lockFile: "npm-shrinkwrap.json",
            cancellationToken: this.CancellationToken()
        );

        NodeRepository repository = await this.DetectRepositoryAsync();

        NodeInstallRoot root = Assert.Single(repository.InstallRoots);
        Assert.Equal(expected: "src", actual: root.RelativeDirectory);
        Assert.Equal(expected: "src/npm-shrinkwrap.json", actual: root.LockFile);
        Assert.Equal(expected: "src", actual: Assert.Single(repository.Packages).RelativeDirectory);
    }

    [Fact]
    public async Task NoPackageJsonIsNotDetectedAsync()
    {
        _ = await this._repository.WriteAsync(
            relative: "src/Example.slnx",
            content: "<Solution />",
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "No package.json at the repository root or in src/", actual: detection.Reason);
    }

    [Fact]
    public async Task NoLockFileIsNotDetectedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: NpmRepository.SAMPLE_MANIFEST,
            lockFile: null,
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(
            expected: "The repository root has no package-lock.json (npm ci needs one)",
            actual: detection.Reason
        );
    }

    [Theory]
    [InlineData("bun.lock", "bun")]
    [InlineData("bun.lockb", "bun")]
    [InlineData("pnpm-lock.yaml", "pnpm")]
    [InlineData("yarn.lock", "yarn")]
    public async Task AnotherPackageManagersLockFileIsUnsupportedAsync(string lockFile, string manager)
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "src",
            manifest: NpmRepository.SAMPLE_MANIFEST,
            lockFile: lockFile,
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(
            expected: "src/ uses " + manager + " (" + lockFile + "); only npm is supported",
            actual: detection.Reason
        );
    }

    [Fact]
    public async Task AnNpmLockFileWinsOverABunOneAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        _ = await this._repository.WriteAsync(
            relative: "bun.lock",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );

        NodeRepository repository = await this.DetectRepositoryAsync();

        Assert.Equal(expected: "package-lock.json", actual: Assert.Single(repository.InstallRoots).LockFile);
    }

    [Fact]
    public async Task APackageManagerFieldNamingAnotherManagerIsUnsupportedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "example", "packageManager": "pnpm@9.0.0" }""",
            lockFile: null,
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(
            expected: "The repository root names another package manager in packageManager; only npm is supported",
            actual: detection.Reason
        );
    }

    [Fact]
    public async Task ArrayWorkspacesWithAGlobAndALiteralAreExpandedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "root", "workspaces": ["packages/*", "./tools/cli", "../outside", "!packages/skip", "deep/**"] }""",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );
        await this.WritePackageAsync(
            directory: "packages/alpha",
            manifest: """{ "name": "@example/alpha", "scripts": { "lint": "eslint ." } }"""
        );
        await this.WritePackageAsync(directory: "packages/beta", manifest: "{}");
        await this.WritePackageAsync(directory: "packages/node_modules", manifest: """{ "name": "ignored" }""");
        await this.WritePackageAsync(directory: "tools/cli", manifest: """{ "name": "cli" }""");
        await this.WritePackageAsync(directory: "outside", manifest: """{ "name": "outside" }""");

        NodeDetection detection = await this.DetectAsync();
        NodeRepository repository = Assert.IsType<NodeRepository>(detection.Repository);

        Assert.Equal(
            expected: ["root", "@example/alpha", "packages/beta", "cli"],
            actual: repository.Packages.Select(static package => package.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ["lint"], actual: repository.Packages[1].Scripts, comparer: StringComparer.Ordinal);
        Assert.Equal(
            expected: ["", "src", ".ai", "packages", "packages/alpha", "packages/beta", "tools/cli"],
            actual: detection.WatchedDirectories,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task ObjectWorkspacesAreExpandedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "root", "workspaces": { "packages": ["libs/*"] } }""",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );
        await this.WritePackageAsync(directory: "libs/core", manifest: """{ "name": "core" }""");

        NodeRepository repository = await this.DetectRepositoryAsync();

        Assert.Equal(
            expected: ["root", "core"],
            actual: repository.Packages.Select(static package => package.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task WorkspacePackagesRecordTheirInstallRootDependenciesAndRunnerAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "root", "workspaces": ["packages/*"], "scripts": { "test": "node --test", "check": "tsc" } }""",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );
        await this.WritePackageAsync(
            directory: "packages/web",
            manifest: """
            {
              "name": "web",
              "scripts": { "test": "npm run unit", "test:noe2e": "jest --ci" },
              "dependencies": { "react": "^19.0.0" },
              "devDependencies": { "jest": "^30.0.0" },
              "optionalDependencies": { "fsevents": "*" }
            }
            """
        );

        NodeRepository repository = await this.DetectRepositoryAsync();

        NodePackage root = repository.Packages[0];
        Assert.True(condition: root.IsInstallRoot, userMessage: "the root package is its own install root");
        Assert.Equal(expected: NodeTestRunner.NodeTest, actual: root.TestRunner);
        Assert.Equal(expected: ["test", "check"], actual: root.ScriptNames, comparer: StringComparer.Ordinal);

        NodePackage web = repository.Packages[1];
        Assert.Equal(expected: ".", actual: web.InstallRoot);
        Assert.False(condition: web.IsInstallRoot, userMessage: "a workspace installs from its root");
        Assert.Equal(expected: NodeTestRunner.Jest, actual: web.TestRunner);
        Assert.Equal(expected: DependencySection.Dependencies, actual: web.FindDependency("react")?.Section);
        Assert.Equal(expected: DependencySection.DevDependencies, actual: web.FindDependency("jest")?.Section);
        Assert.Equal(expected: DependencySection.OptionalDependencies, actual: web.FindDependency("fsevents")?.Section);
        Assert.Null(web.FindDependency("vitest"));
    }

    [Fact]
    public async Task TheTestScriptOverrideIsCarriedToTheRepositoryAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync("""{ "node": { "testScript": "test:unit" } }""");

        NodeRepository repository = await this.DetectRepositoryAsync();

        Assert.Equal(expected: "test:unit", actual: repository.TestScript);
    }

    [Fact]
    public async Task AnInvalidManifestIsReportedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: "{ not json",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "package.json is not valid JSON", actual: detection.Reason);
    }

    [Fact]
    public async Task AnOversizedManifestIsReportedAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: "{\"name\":\"" + new string(c: 'x', count: 1024 * 1024) + "\"}",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "package.json is larger than 1 MiB", actual: detection.Reason);
    }

    [Fact]
    public async Task OverridesCanDisableNodeAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync("""{ "node": { "enabled": false } }""");

        NodeDetection detection = await this.DetectAsync();

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "Disabled by .ai/commands.json", actual: detection.Reason);
    }

    [Fact]
    public async Task OverridesNarrowThePackagesAndSetTheTimeoutAsync()
    {
        await NpmRepository.WriteAsync(
            repository: this._repository,
            directory: "",
            manifest: """{ "name": "root", "workspaces": ["packages/*"] }""",
            lockFile: NpmRepository.NPM_LOCK_FILE,
            cancellationToken: this.CancellationToken()
        );
        await this.WritePackageAsync(directory: "packages/alpha", manifest: """{ "name": "alpha" }""");
        await this.WriteOverridesAsync("""{ "node": { "packages": ["alpha", "missing"], "timeoutMinutes": 500 } }""");

        NodeDetection detection = await this.DetectAsync();
        NodeRepository repository = Assert.IsType<NodeRepository>(detection.Repository);

        Assert.Equal(expected: "alpha", actual: Assert.Single(repository.Packages).Name);
        Assert.Equal(expected: TimeSpan.FromMinutes(240), actual: repository.Timeout);
        Assert.Equal(
            expected: "node.packages names packages that were not found: missing",
            actual: detection.OverridesError
        );
    }

    [Fact]
    public async Task ANodeOverridesErrorIsReportedWithoutStoppingDetectionAsync()
    {
        await NpmRepository.WriteAsync(repository: this._repository, cancellationToken: this.CancellationToken());
        await this.WriteOverridesAsync("""{ "node": { "unknownSetting": true } }""");

        NodeDetection detection = await this.DetectAsync();

        Assert.NotNull(detection.Repository);
        Assert.NotNull(detection.OverridesError);
    }

    [Fact]
    public async Task AMissingRepositoryRootIsNotDetectedAsync()
    {
        NodeDetection detection = await NodeDetector.DetectAsync(
            repositoryRoot: this._repository.Combine("missing"),
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(detection.Repository);
        Assert.Equal(expected: "The repository root does not exist", actual: detection.Reason);
    }

    private async ValueTask WritePackageAsync(string directory, string manifest)
    {
        _ = await this._repository.WriteAsync(
            relative: directory + "/package.json",
            content: manifest,
            cancellationToken: this.CancellationToken()
        );
    }

    private async ValueTask WriteOverridesAsync(string content)
    {
        _ = await this._repository.WriteAsync(
            relative: ".ai/commands.json",
            content: content,
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask<NodeDetection> DetectAsync()
    {
        return NodeDetector.DetectAsync(
            repositoryRoot: this._repository.Path,
            cancellationToken: this.CancellationToken()
        );
    }

    private async ValueTask<NodeRepository> DetectRepositoryAsync()
    {
        NodeDetection detection = await this.DetectAsync();

        return Assert.IsType<NodeRepository>(detection.Repository);
    }
}
