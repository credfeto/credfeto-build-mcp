using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Commands;

public sealed class NpmCommandsTests : TestBase
{
    private const string ROOT = "/work/repo";
    private const string NPM = TestTools.NPM;

    [Fact]
    public void CiIsACleanQuietInstallThatStopsTheJob()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.Ci(repository: repository, installRoot: repository.InstallRoots[0], npm: NPM);

        Assert.Equal(expected: NPM, actual: step.FileName);
        Assert.Equal(expected: ROOT, actual: step.WorkingDirectory);
        Assert.Equal(
            expected: ["ci", "--ignore-scripts", "--no-audit", "--no-fund"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: step.StopJobOnFailure, userMessage: "a failed install stops the job");
    }

    [Fact]
    public void EveryStepRunsWithCiAndNoColour()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.RunScript(
            repository: repository,
            package: repository.Packages[0],
            script: "test",
            npm: NPM
        );

        Assert.Equal(expected: "true", actual: step.Environment["CI"]);
        Assert.Equal(expected: "1", actual: step.Environment["NO_COLOR"]);
        Assert.Equal(expected: "0", actual: step.Environment["FORCE_COLOR"]);
        Assert.Equal(expected: "false", actual: step.Environment["npm_config_update_notifier"]);
    }

    [Fact]
    public void TheRootPackageRunsItsScriptWithoutAWorkspace()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.RunScript(
            repository: repository,
            package: repository.Packages[0],
            script: "lint",
            npm: NPM
        );

        Assert.Equal(expected: ["run", "lint"], actual: step.Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(expected: ROOT, actual: step.WorkingDirectory);
        Assert.Equal(expected: "lint " + SampleWorkspace.ROOT_NAME, actual: step.Name);
        Assert.False(condition: step.StopJobOnFailure, userMessage: "a script failure is the result, not a stop");
    }

    [Fact]
    public void AWorkspacePackageRunsFromItsInstallRootByDirectory()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.RunScript(
            repository: repository,
            package: repository.Packages[1],
            script: "build",
            npm: NPM
        );

        Assert.Equal(
            expected: ["run", "build", "--workspace=packages/web"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ROOT, actual: step.WorkingDirectory);
    }

    [Fact]
    public void APathFilterFollowsTheScriptFromTheInstallRoot()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.RunScript(
            repository: repository,
            run: new ScriptRun(Package: repository.Packages[0], Script: NodeScripts.TEST_NO_E2E)
            {
                PathFilter = "packages/core",
            },
            npm: NPM
        );

        Assert.Equal(
            expected: ["run", "test:noe2e", "--", "packages/core"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ROOT, actual: step.WorkingDirectory);
        Assert.Equal(expected: "test:noe2e " + SampleWorkspace.ROOT_NAME + " packages/core", actual: step.Name);
    }

    [Fact]
    public void AWorkspaceUnderTheSourceInstallRootIsRelativeToIt()
    {
        NodeRepository repository = new(
            RepositoryRoot: ROOT,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: "src", LockFile: "src/package-lock.json")],
            Packages:
            [
                new NodePackage(Name: "app", RelativeDirectory: "src/apps/app", Scripts: ["test"])
                {
                    InstallRoot = "src",
                },
            ],
            Timeout: TimeSpan.FromMinutes(1)
        );

        ProcessStep step = NpmCommands.RunScript(
            repository: repository,
            package: repository.Packages[0],
            script: "test",
            npm: NPM
        );

        Assert.Equal(
            expected: ["run", "test", "--workspace=apps/app"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: Path.Combine(path1: ROOT, path2: "src"), actual: step.WorkingDirectory);
    }

    [Theory]
    [InlineData(DependencySection.Dependencies, "--save-prod")]
    [InlineData(DependencySection.DevDependencies, "--save-dev")]
    [InlineData(DependencySection.OptionalDependencies, "--save-optional")]
    public static void InstallSavesToTheSectionAndSkipsScripts(DependencySection section, string option)
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.Install(
            repository: repository,
            package: repository.Packages[1],
            specification: "react@^19.0.0",
            section: section,
            npm: NPM
        );

        Assert.Equal(
            expected:
            [
                "install",
                "react@^19.0.0",
                option,
                "--workspace=packages/web",
                "--ignore-scripts",
                "--no-audit",
                "--no-fund",
            ],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: step.StopJobOnFailure, userMessage: "a failed install stops the job");
    }

    [Fact]
    public void ListShowsTheInstalledVersionInThePackage()
    {
        NodeRepository repository = SampleWorkspace.Create(ROOT);

        ProcessStep step = NpmCommands.List(
            repository: repository,
            package: repository.Packages[2],
            packageId: "left-pad",
            npm: NPM
        );

        Assert.Equal(
            expected: ["ls", "left-pad", "--depth=0", "--workspace=packages/core"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void AMissingNodeModulesNeedsAnInstall()
    {
        using TemporaryDirectory folder = new();
        NodeRepository repository = SampleWorkspace.Create(folder.Path);

        Assert.True(
            condition: NpmCommands.NeedsInstall(repository: repository, installRoot: repository.InstallRoots[0]),
            userMessage: "nothing is installed yet"
        );
        Assert.Single(
            NpmCommands.InstallIfNeeded(repository: repository, installRoot: repository.InstallRoots[0], npm: NPM)
        );
    }

    [Fact]
    public async Task AnUpToDateInstallIsNotRepeatedAsync()
    {
        using TemporaryDirectory folder = new();
        NodeRepository repository = SampleWorkspace.Create(folder.Path);
        string lockFile = await folder.WriteAsync(
            relative: "package-lock.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        string installed = await folder.WriteAsync(
            relative: "node_modules/.package-lock.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        File.SetLastWriteTimeUtc(path: lockFile, lastWriteTimeUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(
            path: installed,
            lastWriteTimeUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        );

        Assert.Empty(
            NpmCommands.InstallIfNeeded(repository: repository, installRoot: repository.InstallRoots[0], npm: NPM)
        );
    }

    [Fact]
    public async Task ALockFileNewerThanTheInstallNeedsAnInstallAsync()
    {
        using TemporaryDirectory folder = new();
        NodeRepository repository = SampleWorkspace.Create(folder.Path);
        string lockFile = await folder.WriteAsync(
            relative: "package-lock.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        string installed = await folder.WriteAsync(
            relative: "node_modules/.package-lock.json",
            content: "{}",
            cancellationToken: this.CancellationToken()
        );
        File.SetLastWriteTimeUtc(
            path: installed,
            lastWriteTimeUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        File.SetLastWriteTimeUtc(path: lockFile, lastWriteTimeUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        ProcessStep step = Assert.Single(
            NpmCommands.InstallIfNeeded(repository: repository, installRoot: repository.InstallRoots[0], npm: NPM)
        );
        Assert.Equal(expected: "ci", actual: step.Arguments[0]);
    }
}
