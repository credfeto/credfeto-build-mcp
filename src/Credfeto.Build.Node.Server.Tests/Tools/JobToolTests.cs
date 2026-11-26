using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
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
            expected: ["run", "test:noe2e", "--workspace=packages/web"],
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
            expected: ["run", "test:noe2e", "--", "packages/core"],
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

    private static string Text(CallToolResult result)
    {
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(static block => block.Text));
    }
}
