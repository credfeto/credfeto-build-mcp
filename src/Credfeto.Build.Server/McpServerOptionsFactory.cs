using System.Reflection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server;

public static class McpServerOptionsFactory
{
    public static McpServerOptions Create(
        string name,
        Assembly versionSource,
        string instructions,
        McpServerPrimitiveCollection<McpServerTool> tools
    )
    {
        return new()
        {
            ServerInfo = new() { Name = name, Version = Version(versionSource) },
            ServerInstructions = instructions,

            // The live collection, always set even when empty, so tools/list answers with no tools rather than an
            // unknown-method error, and a change to it is sent to the client as notifications/tools/list_changed
            Capabilities = new() { Tools = new() { ListChanged = true } },
            ToolCollection = tools,
        };
    }

    private static string Version(Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    }
}
