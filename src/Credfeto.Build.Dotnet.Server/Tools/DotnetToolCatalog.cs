using System.Collections.Generic;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Server.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Tools;

public static class DotnetToolCatalog
{
    public static IReadOnlyList<McpServerTool> Create(DotnetDetection detection, ToolContext context)
    {
        if (detection.Repository is not { } repository)
        {
            return [];
        }

        List<McpServerTool> tools =
        [
            new DetectTool(context),
            new PackageVersionsTool(context),
            new RestoreTool(context),
            new BuildSolutionTool(context),
        ];

        if (repository.UnitTestProjects.Count != 0)
        {
            tools.Add(new TestTool(repository: repository, context: context));
            tools.Add(new CoverageTool(repository: repository, context: context));
        }

        tools.Add(new BuildCheckTool(context));
        tools.Add(new CleanupTool(context));
        tools.Add(new ChangelogLintTool(context));
        tools.Add(new BuildtestTool(context));
        tools.Add(new JobStatusTool(context.Runner));
        tools.Add(new JobResultTool(context.Runner));

        return tools;
    }
}
