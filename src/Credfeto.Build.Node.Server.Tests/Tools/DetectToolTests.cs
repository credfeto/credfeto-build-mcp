using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tests.Helpers;
using Credfeto.Build.Node.Server.Tools;
using Credfeto.Build.Server.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Node.Server.Tests.Tools;

public sealed class DetectToolTests : TestBase
{
    private const string ROOT = "/work/repo";

    [Fact]
    public void NoRepositoryOffersNoTools()
    {
        IReadOnlyList<McpServerTool> tools = Catalog(NotDetected());

        Assert.Empty(tools);
    }

    [Fact]
    public void AnNpmRepositoryOffersDetectOnly()
    {
        IReadOnlyList<McpServerTool> tools = Catalog(Detected());

        Assert.Equal(
            expected: [ToolNames.DETECT],
            actual: tools.Select(static tool => tool.ProtocolTool.Name),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task DetectReportsTheInstallRootsAndPackagesAsync()
    {
        BuildTool detect = Assert.IsType<DetectTool>(Assert.Single(Catalog(Detected())));

        CallToolResult result = await detect.CallAsync(arguments: null, cancellationToken: this.CancellationToken());

        Assert.False(condition: result.IsError, userMessage: Text(result));
        using JsonDocument document = JsonDocument.Parse(Text(result));
        JsonElement root = document.RootElement;
        Assert.Equal(expected: "npm", actual: root.GetProperty("packageManager").GetString());
        Assert.Equal(
            expected: "package-lock.json",
            actual: root.GetProperty("installRoots")[0].GetProperty("lockFile").GetString()
        );
        JsonElement package = root.GetProperty("packages")[0];
        Assert.Equal(expected: "example", actual: package.GetProperty("name").GetString());
        Assert.Equal(expected: ".", actual: package.GetProperty("directory").GetString());
        Assert.Equal(expected: "test", actual: package.GetProperty("scripts")[0].GetString());
        Assert.Equal(expected: 30, actual: root.GetProperty("timeoutMinutes").GetInt32());
        Assert.False(
            condition: root.TryGetProperty(propertyName: "overridesError", value: out _),
            userMessage: "no overrides error"
        );
    }

    [Fact]
    public async Task DetectFailsOnceThePackagesHaveGoneAsync()
    {
        DetectTool detect = new(new ToolContext(new FixedDetectionSource(NotDetected())));

        CallToolResult result = await detect.CallAsync(arguments: null, cancellationToken: this.CancellationToken());

        Assert.True(condition: result.IsError, userMessage: "detect reports the repository has gone");
        Assert.Contains(
            expectedSubstring: "no package-lock.json",
            actualString: Text(result),
            comparisonType: StringComparison.Ordinal
        );
    }

    private static IReadOnlyList<McpServerTool> Catalog(NodeDetection detection)
    {
        return NodeToolCatalog.Create(
            detection: detection,
            context: new ToolContext(new FixedDetectionSource(detection))
        );
    }

    private static NodeDetection Detected()
    {
        NodeRepository repository = new(
            RepositoryRoot: ROOT,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages: [new NodePackage(Name: "example", RelativeDirectory: ".", Scripts: ["test"])],
            Timeout: TimeSpan.FromMinutes(30)
        );

        return new(
            RepositoryRoot: ROOT,
            Repository: repository,
            Reason: "npm packages found",
            OverridesError: null,
            WatchedDirectories: NodeDetector.BaseWatchedDirectories
        );
    }

    private static NodeDetection NotDetected()
    {
        return new(
            RepositoryRoot: ROOT,
            Repository: null,
            Reason: "The repository root has no package-lock.json (npm ci needs one)",
            OverridesError: null,
            WatchedDirectories: NodeDetector.BaseWatchedDirectories
        );
    }

    private static string Text(CallToolResult result)
    {
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(static block => block.Text));
    }
}
