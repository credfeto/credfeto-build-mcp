using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Node.Server.Tools;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Tools;

public sealed class CoverageToolTests : TestBase, IDisposable
{
    private const string ROOT = "example-root";
    private const string WEB = "@example/web";
    private const string CORE = "@example/core";

    private readonly TemporaryDirectory _repository = new();
    private readonly RecordingJobRunner _runner = new();

    public void Dispose()
    {
        this._repository.Dispose();
    }

    [Fact]
    public void NoCoverageScriptMeansNoCoverageTool()
    {
        IReadOnlyList<McpServerTool> tools = this.Tools(SampleWorkspace.Create(this._repository.Path));

        Assert.DoesNotContain(
            expected: ToolNames.COVERAGE,
            collection: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void ATestCoverageScriptOffersTheCoverageTool()
    {
        IReadOnlyList<McpServerTool> tools = this.Tools(this.Workspace(coverageScript: null, coverageFile: null));

        Assert.Contains(
            expected: ToolNames.COVERAGE,
            collection: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void AnOverrideScriptOnlyCountsWhereAPackageHasIt()
    {
        IReadOnlyList<McpServerTool> missing = this.Tools(
            this.Workspace(coverageScript: "coverage:ci", coverageFile: null, rootScripts: [NodeScripts.TEST])
        );
        IReadOnlyList<McpServerTool> present = this.Tools(
            this.Workspace(coverageScript: "coverage:ci", coverageFile: null, rootScripts: ["coverage:ci"])
        );

        Assert.DoesNotContain(
            expected: ToolNames.COVERAGE,
            collection: missing.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Contains(
            expected: ToolNames.COVERAGE,
            collection: present.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task CoverageRunsEachPackagesScriptAndNamesItsReportAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.Workspace(coverageScript: null, coverageFile: null, rootScripts: []),
            arguments: Arguments()
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: ToolNames.COVERAGE, actual: definition.Kind);
        Assert.Equal(
            expected: ["restore .", "test:coverage " + WEB, "test:coverage " + CORE],
            actual: definition.Steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Same(expected: CoverageLanguage.Node, actual: definition.CoverageLanguage);
        Assert.Equal(expected: this.InRepository(CoverageFile.FILE_NAME), actual: definition.CoverageReportPath);

        string webReport = this.InRepository("packages/web/coverage/cobertura-coverage.xml");
        string coreReport = this.InRepository("packages/core/coverage/cobertura-coverage.xml");
        Assert.Equal(
            expected: [webReport, coreReport],
            actual: definition.CoverageFiles,
            comparer: StringComparer.Ordinal
        );
        Assert.NotNull(definition.CoverageComponents);
        Assert.Equal(expected: WEB, actual: definition.CoverageComponents[webReport]);
        Assert.Equal(expected: CORE, actual: definition.CoverageComponents[coreReport]);
    }

    [Fact]
    public async Task ARootCoverageScriptRunsOnceAtTheRootAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.Workspace(coverageScript: null, coverageFile: null),
            arguments: Arguments()
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(
            expected: [this.InRepository("coverage/cobertura-coverage.xml")],
            actual: definition.CoverageFiles,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task TheCoverageFileOverrideIsReadInsideEachPackageAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.Workspace(coverageScript: null, coverageFile: "reports/cobertura.xml", rootScripts: []),
            arguments: Arguments(("package", WEB))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.Equal(
            expected: [this.InRepository("packages/web/reports/cobertura.xml")],
            actual: Assert.Single(this._runner.Started).CoverageFiles,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void TheCoverageSchemaHasNoWayToSkipTheReport()
    {
        BuildTool tool = TestTools.Find(
            tools: this.Tools(this.Workspace(coverageScript: null, coverageFile: null)),
            name: ToolNames.COVERAGE
        );

        Assert.Equal(
            expected: ["package"],
            actual: tool.ProtocolTool.InputSchema.GetProperty("properties")
                .EnumerateObject()
                .Select(static property => property.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task CoverageOfAllPackagesAlwaysWritesCoverageMarkdownAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.Workspace(coverageScript: null, coverageFile: null),
            arguments: Arguments(("package", "all"))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: this.InRepository(CoverageFile.FILE_NAME), actual: definition.CoverageReportPath);
        Assert.Null(definition.CoverageReportComponent);
    }

    [Fact]
    public async Task CoverageOfOnePackageUpdatesOnlyItsRowAsync()
    {
        CallToolResult result = await this.CallAsync(
            repository: this.Workspace(coverageScript: null, coverageFile: null, rootScripts: []),
            arguments: Arguments(("package", WEB))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        JobDefinition definition = Assert.Single(this._runner.Started);
        Assert.Equal(expected: this.InRepository(CoverageFile.FILE_NAME), actual: definition.CoverageReportPath);
        Assert.Equal(expected: WEB, actual: definition.CoverageReportComponent);
    }

    [Fact]
    public async Task AStaleReportIsDeletedBeforeTheRunAsync()
    {
        string stale = this.InRepository("packages/web/coverage/cobertura-coverage.xml");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        await File.WriteAllTextAsync(
            path: stale,
            contents: "<coverage />",
            cancellationToken: this.CancellationToken()
        );

        CallToolResult result = await this.CallAsync(
            repository: this.Workspace(coverageScript: null, coverageFile: null, rootScripts: []),
            arguments: Arguments(("package", WEB))
        );

        Assert.False(condition: result.IsError, userMessage: Text(result));
        Assert.False(condition: File.Exists(stale), userMessage: "an earlier run's report must not be read");
    }

    private NodeRepository Workspace(string? coverageScript, string? coverageFile)
    {
        return this.Workspace(
            coverageScript: coverageScript,
            coverageFile: coverageFile,
            rootScripts: [NodeScripts.TEST_COVERAGE]
        );
    }

    private NodeRepository Workspace(string? coverageScript, string? coverageFile, string[] rootScripts)
    {
        string[] packageScripts = [NodeScripts.TEST, NodeScripts.TEST_COVERAGE];

        return new(
            RepositoryRoot: this._repository.Path,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages:
            [
                new NodePackage(Name: ROOT, RelativeDirectory: ".", Scripts: rootScripts) { ScriptNames = rootScripts },
                new NodePackage(Name: WEB, RelativeDirectory: "packages/web", Scripts: packageScripts)
                {
                    ScriptNames = packageScripts,
                    TestRunner = NodeTestRunner.Vitest,
                },
                new NodePackage(Name: CORE, RelativeDirectory: "packages/core", Scripts: packageScripts)
                {
                    ScriptNames = packageScripts,
                    TestRunner = NodeTestRunner.Vitest,
                },
            ],
            Timeout: TimeSpan.FromMinutes(30)
        )
        {
            CoverageScript = coverageScript,
            CoverageFile = coverageFile,
        };
    }

    private string InRepository(string relative)
    {
        return Path.Combine(
            path1: this._repository.Path,
            path2: relative.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar)
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

    private ValueTask<CallToolResult> CallAsync(NodeRepository repository, Dictionary<string, JsonElement> arguments)
    {
        BuildTool tool = TestTools.Find(tools: this.Tools(repository), name: ToolNames.COVERAGE);

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
