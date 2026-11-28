using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Testing;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Node.Server.Tools;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Tools;

public sealed class JobToolTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _repository = new();
    private readonly RecordingJobRunner _runner = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public void TheToolsFollowTheScripts()
    {
        IReadOnlyList<McpServerTool> tools = this.Tools(SampleWorkspace.Create(this._repository.Path));

        Assert.Equal(
            expected:
            [
                ToolNames.DETECT,
                ToolNames.RESTORE,
                ToolNames.BUILD,
                ToolNames.LINT,
                ToolNames.TEST,
                ToolNames.ADD_PACKAGE,
                ToolNames.UPDATE_PACKAGE,
                "job_status",
                "job_result",
            ],
            actual: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void APackageWithNoScriptsGetsNoScriptTools()
    {
        NodeRepository repository = new(
            RepositoryRoot: this._repository.Path,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages: [new NodePackage(Name: "plain", RelativeDirectory: ".", Scripts: [])],
            Timeout: TimeSpan.FromMinutes(5)
        );

        IReadOnlyList<McpServerTool> tools = this.Tools(repository);

        Assert.DoesNotContain(
            expected: ToolNames.BUILD,
            collection: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.DoesNotContain(
            expected: ToolNames.TEST,
            collection: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.DoesNotContain(
            expected: ToolNames.TYPECHECK,
            collection: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task RestoreRunsACleanInstallInEveryInstallRootAsync()
    {
        CallToolResult result = await this.CallAsync(name: ToolNames.RESTORE, arguments: Arguments());

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        ProcessStep step = Assert.Single(definition.Steps);
        Assert.Equal(expected: TestTools.NPM, actual: step.FileName);
        Assert.Equal(expected: "ci", actual: step.Arguments[0]);
        Assert.Null(definition.TestParser);
    }

    [Fact]
    public async Task TestInstallsFirstWhenNothingIsInstalledAsync()
    {
        CallToolResult result = await this.CallAsync(name: ToolNames.TEST, arguments: Arguments());

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["restore .", "test " + SampleWorkspace.ROOT_NAME],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.True(condition: definition.ContinueOnFailure, userMessage: "every package's tests run");
        Assert.IsType<CompositeTestOutputParser>(definition.TestParser);
    }

    [Fact]
    public async Task TestOfAWorkspacePackageUsesItsNoE2eScriptAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.TEST,
            arguments: Arguments(("package", SampleWorkspace.WEB))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep step = Assert.Single(this._runner.Started).Steps[^1];
        Assert.Equal(
            expected:
            [
                "run",
                "test:noe2e",
                "--workspace=packages/web",
                "--",
                "--maxWorkers=4",
                "--workerIdleMemoryLimit=1024MiB",
            ],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task UnitTestOfAWorkspaceWithoutNoE2eIsRefusedWithoutAJobAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.TEST,
            arguments: Arguments(("package", SampleWorkspace.CORE))
        );

        Assert.True(condition: result.IsError, userMessage: "unit never runs a workspace's test script");
        Assert.Equal(expected: TestTool.NO_UNIT_SCRIPT, actual: Text(result));
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task AllScopeTestOfAWorkspaceRunsItsTestScriptAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.TEST,
            arguments: Arguments(("package", SampleWorkspace.CORE), ("scope", TestScripts.SCOPE_ALL))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: ["run", "test", "--workspace=packages/core"],
            actual: Assert.Single(this._runner.Started).Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task UnitTestOfAWorkspaceRunsTheInstallRootNoE2eForItsDirectoryAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: SampleWorkspace.Create(
                repositoryRoot: this._repository.Path,
                rootScripts: [NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
            ),
            name: ToolNames.TEST,
            arguments: Arguments(("package", SampleWorkspace.CORE))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        ProcessStep step = definition.Steps[^1];
        Assert.Equal(
            expected: ["run", "test:noe2e", "--", "--maxWorkers=4", "packages/core"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: this._repository.Path, actual: step.WorkingDirectory);
        Assert.Equal(
            expected: ["restore .", "test:noe2e " + SampleWorkspace.ROOT_NAME + " packages/core"],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task UnitTestOfAllWithNothingToRunIsRefusedWithoutAJobAsync()
    {
        NodeRepository repository = new(
            RepositoryRoot: this._repository.Path,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages:
            [
                new NodePackage(Name: "root", RelativeDirectory: ".", Scripts: []),
                new NodePackage(Name: "app", RelativeDirectory: "packages/app", Scripts: [NodeScripts.TEST]),
            ],
            Timeout: TimeSpan.FromMinutes(5)
        );

        CallToolResult result = await this.CallAsync(
            repository: repository,
            name: ToolNames.TEST,
            arguments: Arguments()
        );

        Assert.True(condition: result.IsError, userMessage: "nothing runs under unit");
        Assert.Contains(
            expectedSubstring: "No package has the script",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task TestPathsAreHandedToTheRunnerAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments(("package", SampleWorkspace.WEB));
        arguments["paths"] = Paths("src/a.test.ts", "src/b.test.ts");

        CallToolResult result = await this.CallAsync(name: ToolNames.TEST, arguments: arguments);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected:
            [
                "run",
                "test:noe2e",
                "--workspace=packages/web",
                "--",
                "--maxWorkers=4",
                "--workerIdleMemoryLimit=1024MiB",
                "src/a.test.ts",
                "src/b.test.ts",
            ],
            actual: definition.Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Null(definition.Note);
    }

    [Fact]
    public async Task TestCoverageKeepsTheInstallRootDirectoryFilterAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments(("package", SampleWorkspace.CORE));
        arguments["coverage"] = JsonSerializer.SerializeToElement(
            value: true,
            jsonTypeInfo: TestJsonContext.Default.Boolean
        );

        CallToolResult result = await this.CallAsync(
            repository: SampleWorkspace.Create(
                repositoryRoot: this._repository.Path,
                rootScripts: [NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
            ),
            name: ToolNames.TEST,
            arguments: arguments
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: ["run", "test:noe2e", "--", "--maxWorkers=2", "--coverage", "packages/core"],
            actual: Assert.Single(this._runner.Started).Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task TestRefusesAnUnsafePathWithoutAJobAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments(("package", SampleWorkspace.WEB));
        arguments["paths"] = Paths("src/a.test.ts", "../secret.test.ts");

        CallToolResult result = await this.CallAsync(name: ToolNames.TEST, arguments: arguments);

        Assert.True(condition: result.IsError, userMessage: "a path climbing out is refused");
        Assert.Contains(
            expectedSubstring: "Invalid value for paths",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task TestOfAnUnknownRunnerWithPathsCarriesANoteAsync()
    {
        NodeRepository repository = new(
            RepositoryRoot: this._repository.Path,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages:
            [
                new NodePackage(Name: "app", RelativeDirectory: ".", Scripts: [NodeScripts.TEST])
                {
                    ScriptNames = [NodeScripts.TEST],
                },
            ],
            Timeout: TimeSpan.FromMinutes(5)
        );
        Dictionary<string, JsonElement> arguments = Arguments(("scope", TestScripts.SCOPE_ALL));
        arguments["paths"] = Paths("a.test.js");
        arguments["coverage"] = JsonSerializer.SerializeToElement(
            value: true,
            jsonTypeInfo: TestJsonContext.Default.Boolean
        );

        CallToolResult result = await this.CallAsync(
            repository: repository,
            name: ToolNames.TEST,
            arguments: arguments
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["run", "test", "--", "a.test.js"],
            actual: definition.Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: "The test runner of app is not known, so the paths were passed to its test script after -- and may have been ignored, and no coverage switch was passed",
            actual: definition.Note
        );
    }

    [Fact]
    public async Task TestRecordsTheWorkerBudgetOnTheJobAsync()
    {
        CallToolResult result = await this.CallAsync(name: ToolNames.TEST, arguments: Arguments());

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: new ResourceNote(
                Workers: 4,
                MemoryPerWorkerMb: 1024,
                MemoryLimitMb: 4096,
                MemoryLimitSource: "cgroup v2"
            ),
            actual: Assert.Single(this._runner.Started).Resources
        );
    }

    [Fact]
    public async Task TestOfANodeTestPackageGetsNoWorkerArgumentsAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.TEST,
            arguments: Arguments(("package", SampleWorkspace.CORE), ("scope", TestScripts.SCOPE_ALL))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.DoesNotContain(Assert.Single(this._runner.Started).Steps[^1].Arguments, static argument =>
            argument.StartsWith(value: "--maxWorkers", comparisonType: StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task TestUsesTheRepositorysMemoryPerWorkerAsync()
    {
        NodeRepository repository = SampleWorkspace.Create(this._repository.Path) with { MemoryPerWorkerMb = 2048 };

        CallToolResult result = await this.CallAsync(
            repository: repository,
            name: ToolNames.TEST,
            arguments: Arguments(("package", SampleWorkspace.WEB))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected:
            [
                "run",
                "test:noe2e",
                "--workspace=packages/web",
                "--",
                "--maxWorkers=2",
                "--workerIdleMemoryLimit=2048MiB",
            ],
            actual: definition.Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: 2, actual: definition.Resources?.Workers);
        Assert.Equal(expected: 2048, actual: definition.Resources?.MemoryPerWorkerMb);
    }

    [Fact]
    public async Task TestWithNoReadableMemoryLimitRunsAWorkerPerProcessorAsync()
    {
        MachineResources unknown = new(memory: FixedMemorySource.None, processorCount: 3);
        NodeDetection detection = TestTools.Detected(SampleWorkspace.Create(this._repository.Path));
        BuildTool tool = TestTools.Find(
            tools: TestTools.Catalog(
                detection: detection,
                runner: this._runner,
                npm: TestTools.FakeNpm,
                resources: unknown
            ),
            name: ToolNames.TEST
        );

        CallToolResult result = await tool.CallAsync(
            arguments: Arguments(),
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["run", "test", "--", "--maxWorkers=3"],
            actual: definition.Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(
            expected: new ResourceNote(
                Workers: 3,
                MemoryPerWorkerMb: 1024,
                MemoryLimitMb: null,
                MemoryLimitSource: null
            ),
            actual: definition.Resources
        );
    }

    [Fact]
    public async Task TestRejectsAPackageThatIsNotDetectedAsync()
    {
        CallToolResult result = await this.CallAsync(name: ToolNames.TEST, arguments: Arguments(("package", "nope")));

        Assert.True(condition: result.IsError, userMessage: "an unknown package is refused");
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task BuildRunsOnlyThePackagesWithABuildScriptAsync()
    {
        CallToolResult result = await this.CallAsync(name: ToolNames.BUILD, arguments: Arguments());

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: ["restore .", "build " + SampleWorkspace.WEB],
            actual: Assert.Single(this._runner.Started).Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task BuildWithoutArgsPassesNothingAfterTheScriptAsync()
    {
        CallToolResult result = await this.CallAsync(name: ToolNames.BUILD, arguments: Arguments());

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: ["run", "build", "--workspace=packages/web"],
            actual: Assert.Single(this._runner.Started).Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task BuildArgsAreHandedToTheScriptAfterDashDashAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments(("package", SampleWorkspace.WEB));
        arguments[ScriptArguments.NAME] = Paths("handler", "--mode=production", "src/entry.ts");

        CallToolResult result = await this.CallAsync(name: ToolNames.BUILD, arguments: arguments);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected:
            [
                "run",
                "build",
                "--workspace=packages/web",
                "--",
                "handler",
                "--mode=production",
                "src/entry.ts",
            ],
            actual: Assert.Single(this._runner.Started).Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task LintArgsAreHandedToTheScriptAfterDashDashAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments();
        arguments[ScriptArguments.NAME] = Paths("--fix");

        CallToolResult result = await this.CallAsync(name: ToolNames.LINT, arguments: arguments);

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: ["run", "lint", "--", "--fix"],
            actual: Assert.Single(this._runner.Started).Steps[^1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData("handler; rm -rf /")]
    [InlineData("$(whoami)")]
    [InlineData("a b")]
    [InlineData("")]
    public async Task BuildRefusesAnUnsafeArgumentWithoutAJobAsync(string value)
    {
        Dictionary<string, JsonElement> arguments = Arguments();
        arguments[ScriptArguments.NAME] = Paths("handler", value);

        CallToolResult result = await this.CallAsync(name: ToolNames.BUILD, arguments: arguments);

        Assert.True(condition: result.IsError, userMessage: "the argument is refused");
        Assert.Contains(
            expectedSubstring: "Invalid value for args",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task BuildRefusesArgsThatAreNotAListOfStringsAsync()
    {
        Dictionary<string, JsonElement> arguments = Arguments(("args", "handler"));

        CallToolResult result = await this.CallAsync(name: ToolNames.BUILD, arguments: arguments);

        Assert.True(condition: result.IsError, userMessage: "args must be a list");
        Assert.Empty(this._runner.Started);
    }

    [Theory]
    [InlineData(ToolNames.BUILD)]
    [InlineData(ToolNames.LINT)]
    public void ScriptToolsOfferArgsAsAListOfStrings(string name)
    {
        JsonElement args = TestTools
            .Find(tools: this.Tools(SampleWorkspace.Create(this._repository.Path)), name: name)
            .ProtocolTool.InputSchema.GetProperty("properties")
            .GetProperty(ScriptArguments.NAME);

        Assert.Equal(expected: "array", actual: args.GetProperty("type").GetString());
        Assert.Equal(
            expected: ScriptArguments.PATTERN,
            actual: args.GetProperty("items").GetProperty("pattern").GetString()
        );
    }

    [Fact]
    public void TypeCheckIsOfferedForATsConfigAlone()
    {
        IReadOnlyList<McpServerTool> tools = this.Tools(this.TypeCheckRepository());

        Assert.Contains(
            expected: ToolNames.TYPECHECK,
            collection: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task TypeCheckPrefersThePackagesTypecheckScriptAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.TypeCheckRepository(),
            name: ToolNames.TYPECHECK,
            arguments: Arguments(("package", "scripted"))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep step = Assert.Single(this._runner.Started).Steps[^1];
        Assert.Equal(expected: TestTools.NPM, actual: step.FileName);
        Assert.Equal(
            expected: ["run", "typecheck", "--workspace=packages/scripted"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task TypeCheckFallsBackToTheLocalTscAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.TypeCheckRepository(),
            name: ToolNames.TYPECHECK,
            arguments: Arguments(("package", "configured"))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep step = Assert.Single(this._runner.Started).Steps[^1];
        Assert.Equal(
            expected: Path.Combine(
                path1: this._repository.Path,
                path2: NpmCommands.NODE_MODULES,
                path3: ".bin",
                path4: NpmCommands.TSC
            ),
            actual: step.FileName
        );
        Assert.Equal(
            expected: ["--noEmit", "-p", "tsconfig.json"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: this._repository.Combine("packages/configured"), actual: step.WorkingDirectory);
    }

    [Fact]
    public async Task TypeCheckOfAllChecksEveryQualifyingPackageAfterOneInstallAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.TypeCheckRepository(),
            name: ToolNames.TYPECHECK,
            arguments: Arguments()
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: ["restore .", "typecheck scripted", "tsc configured"],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Null(definition.TestParser);
    }

    [Fact]
    public async Task TypeCheckRejectsAPackageWithNeitherAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.TypeCheckRepository(),
            name: ToolNames.TYPECHECK,
            arguments: Arguments(("package", "plain"))
        );

        Assert.True(condition: result.IsError, userMessage: "plain has nothing to check with");
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task AJobNeedsNpmOnThePathAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.RESTORE,
            arguments: Arguments(),
            npm: TestTools.NoNpm
        );

        Assert.True(condition: result.IsError, userMessage: "no npm, no job");
        Assert.Contains(
            expectedSubstring: "npm was not found",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task AddPackageInstallsIntoTheChosenWorkspaceAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.ADD_PACKAGE,
            arguments: Arguments(
                ("package_id", "@types/node"),
                ("version", "^24.0.0"),
                ("package", SampleWorkspace.CORE)
            ),
            dev: true
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected:
            [
                "install",
                "@types/node@^24.0.0",
                "--save-dev",
                "--workspace=packages/core",
                "--ignore-scripts",
                "--no-audit",
                "--no-fund",
            ],
            actual: definition.Steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "ls", actual: definition.Steps[1].Arguments[0]);
    }

    [Fact]
    public async Task AddPackageWithoutAVersionTakesTheLatestAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.ADD_PACKAGE,
            arguments: Arguments(("package_id", "zod"), ("package", SampleWorkspace.ROOT_NAME))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: ["install", "zod", "--save-prod", "--ignore-scripts", "--no-audit", "--no-fund"],
            actual: Assert.Single(this._runner.Started).Steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData("package_id", "https://example.com/evil.tgz")]
    [InlineData("package_id", "../local")]
    [InlineData("package_id", "Uppercase")]
    [InlineData("version", "github:someone/repo")]
    [InlineData("version", "file:../x")]
    [InlineData("version", "1.0.0 --global")]
    public async Task AddPackageRefusesAnythingButARegistryNameAndVersionAsync(string parameter, string value)
    {
        Dictionary<string, JsonElement> arguments = Arguments(
            ("package_id", "zod"),
            ("version", "1.0.0"),
            ("package", SampleWorkspace.ROOT_NAME)
        );
        arguments[parameter] = JsonSerializer.SerializeToElement(
            value: value,
            jsonTypeInfo: TestJsonContext.Default.String
        );

        CallToolResult result = await this.CallAsync(name: ToolNames.ADD_PACKAGE, arguments: arguments);

        Assert.True(condition: result.IsError, userMessage: "the value is refused");
        Assert.Empty(this._runner.Started);
    }

    [Fact]
    public async Task UpdatePackageUpdatesEveryDeclaringPackageInItsSectionAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.UPDATE_PACKAGE,
            arguments: Arguments(("package_id", "vitest"))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected:
            [
                "install vitest@latest " + SampleWorkspace.ROOT_NAME,
                "install vitest@latest " + SampleWorkspace.WEB,
                "list vitest " + SampleWorkspace.ROOT_NAME,
                "list vitest " + SampleWorkspace.WEB,
            ],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Contains(
            expected: "--save-dev",
            collection: definition.Steps[1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task UpdatePackageKeepsAnOptionalDependencyOptionalAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.UPDATE_PACKAGE,
            arguments: Arguments(("package_id", "left-pad"), ("version", "1.3.0"), ("package", SampleWorkspace.CORE))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        ProcessStep install = Assert.Single(this._runner.Started).Steps[0];
        Assert.Contains(expected: "left-pad@1.3.0", collection: install.Arguments, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "--save-optional", collection: install.Arguments, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task UpdatePackageRefusesADependencyNoPackageDeclaresAsync()
    {
        CallToolResult result = await this.CallAsync(
            name: ToolNames.UPDATE_PACKAGE,
            arguments: Arguments(("package_id", "lodash"))
        );

        Assert.True(condition: result.IsError, userMessage: "nothing to update");
        Assert.Contains(
            expectedSubstring: "add_package",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(this._runner.Started);
    }

    // A workspace whose root has neither a typecheck script nor a tsconfig.json, so all reaches every workspace package
    private NodeRepository TypeCheckRepository()
    {
        return new(
            RepositoryRoot: this._repository.Path,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages:
            [
                new NodePackage(Name: "root", RelativeDirectory: ".", Scripts: []),
                new NodePackage(
                    Name: "scripted",
                    RelativeDirectory: "packages/scripted",
                    Scripts: [NodeScripts.TYPECHECK]
                )
                {
                    HasTsConfig = true,
                },
                new NodePackage(Name: "configured", RelativeDirectory: "packages/configured", Scripts: [])
                {
                    HasTsConfig = true,
                },
                new NodePackage(Name: "plain", RelativeDirectory: "packages/plain", Scripts: []),
            ],
            Timeout: TimeSpan.FromMinutes(5)
        );
    }

    private IReadOnlyList<McpServerTool> Tools(NodeRepository repository)
    {
        return TestTools.Catalog(
            detection: TestTools.Detected(repository),
            runner: this._runner,
            npm: TestTools.FakeNpm
        );
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments)
    {
        return this.CallAsync(name: name, arguments: arguments, npm: TestTools.FakeNpm);
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments, bool dev)
    {
        arguments["dev"] = JsonSerializer.SerializeToElement(value: dev, jsonTypeInfo: TestJsonContext.Default.Boolean);

        return this.CallAsync(name: name, arguments: arguments);
    }

    private ValueTask<CallToolResult> CallAsync(string name, Dictionary<string, JsonElement> arguments, NpmLocator npm)
    {
        return this.CallAsync(
            repository: SampleWorkspace.Create(this._repository.Path),
            name: name,
            arguments: arguments,
            npm: npm
        );
    }

    private ValueTask<CallToolResult> CallAsync(
        NodeRepository repository,
        string name,
        Dictionary<string, JsonElement> arguments
    )
    {
        return this.CallAsync(repository: repository, name: name, arguments: arguments, npm: TestTools.FakeNpm);
    }

    private ValueTask<CallToolResult> CallAsync(
        NodeRepository repository,
        string name,
        Dictionary<string, JsonElement> arguments,
        NpmLocator npm
    )
    {
        NodeDetection detection = TestTools.Detected(repository);
        BuildTool tool = TestTools.Find(
            tools: TestTools.Catalog(detection: detection, runner: this._runner, npm: npm),
            name: name
        );

        return tool.CallAsync(arguments: arguments, cancellationToken: this.CancellationToken());
    }

    private static Dictionary<string, JsonElement> Arguments(params (string Name, string Value)[] values)
    {
        Dictionary<string, JsonElement> arguments = [with(StringComparer.Ordinal)];

        foreach ((string name, string value) in values)
        {
            arguments[name] = JsonSerializer.SerializeToElement(
                value: value,
                jsonTypeInfo: TestJsonContext.Default.String
            );
        }

        return arguments;
    }

    private static JsonElement Paths(params string[] paths)
    {
        return JsonSerializer.SerializeToElement(value: paths, jsonTypeInfo: TestJsonContext.Default.StringArray);
    }

    private static string Text(CallToolResult result)
    {
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(static block => block.Text));
    }
}
