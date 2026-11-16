using System.Collections.Generic;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Node.Server.Tools;

public static class NodeToolCatalog
{
    // A tool for a script is offered only when some package has that script; coverage only when some package has a
    // coverage script of its own (see CoverageScripts)
    public static IReadOnlyList<McpServerTool> Create(NodeDetection detection, ToolContext context)
    {
        if (detection.Repository is not { } repository)
        {
            return [];
        }

        List<McpServerTool> tools = [new DetectTool(context), new RestoreTool(context)];

        if (ScriptTool.Offered(repository: repository, script: NodeScripts.BUILD))
        {
            tools.Add(ScriptTool.Build(repository: repository, context: context));
        }

        if (ScriptTool.Offered(repository: repository, script: NodeScripts.LINT))
        {
            tools.Add(ScriptTool.Lint(repository: repository, context: context));
        }

        if (TestTool.Offered(repository))
        {
            tools.Add(new TestTool(repository: repository, context: context));
        }

        if (CoverageTool.Offered(repository))
        {
            tools.Add(new CoverageTool(repository: repository, context: context));
        }

        tools.Add(new AddPackageTool(repository: repository, context: context));
        tools.Add(new UpdatePackageTool(repository: repository, context: context));
        tools.Add(new JobStatusTool(context.Runner));
        tools.Add(new JobResultTool(context.Runner));

        return tools;
    }
}
