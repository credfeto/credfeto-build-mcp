using System.Collections.Generic;
using Credfeto.Build.Node.Server.Detection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Node.Server.Tools;

public static class NodeToolCatalog
{
    public static IReadOnlyList<McpServerTool> Create(NodeDetection detection, ToolContext context)
    {
        if (detection.Repository is null)
        {
            return [];
        }

        return [new DetectTool(context)];
    }
}
