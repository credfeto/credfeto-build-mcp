using System.Reflection;
using Credfeto.Build.Server.Tests.Helpers;
using FunFair.Test.Common;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Build.Server.Tests.Startup;

public sealed class McpServerOptionsFactoryTests : TestBase
{
    [Fact]
    public static void TheOptionsNameTheServerAndServeTheLiveToolCollection()
    {
        McpServerPrimitiveCollection<McpServerTool> tools = [new FakeTool("detect")];
        Assembly assembly = typeof(McpServerOptionsFactoryTests).Assembly;

        McpServerOptions options = McpServerOptionsFactory.Create(
            name: "example-server",
            versionSource: assembly,
            instructions: "Does things",
            tools: tools
        );

        Assert.Equal(expected: "example-server", actual: options.ServerInfo?.Name);
        Assert.Equal(
            expected: assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            actual: options.ServerInfo?.Version
        );
        Assert.Equal(expected: "Does things", actual: options.ServerInstructions);
        Assert.True(condition: options.Capabilities?.Tools?.ListChanged, userMessage: "list changes are advertised");
        Assert.Same(expected: tools, actual: options.ToolCollection);
    }
}
