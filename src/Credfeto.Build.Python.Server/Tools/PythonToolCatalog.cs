using System.Collections.Generic;
using Credfeto.Build.Python.Server.Detection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Python.Server.Tools;

public static class PythonToolCatalog
{
    // Strict detection: no tools without a manifest. A repository with Python test files that nothing declares still
    // gets detect, which says where they are
    public static IReadOnlyList<McpServerTool> Create(PythonDetection detection, ToolContext context)
    {
        if (detection.Repository is null && detection.TestSuite is null)
        {
            return [];
        }

        return [new DetectTool(context)];
    }
}
