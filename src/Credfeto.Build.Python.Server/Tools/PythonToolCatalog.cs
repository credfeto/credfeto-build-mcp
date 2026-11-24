using System.Collections.Generic;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Server.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Python.Server.Tools;

public static class PythonToolCatalog
{
    // Strict detection: no tools without a manifest. A repository with Python test files that nothing declares gets
    // detect and restore, which creates the manifest; one whose projects are all unsupported gets only detect
    public static IReadOnlyList<McpServerTool> Create(PythonDetection detection, ToolContext context)
    {
        if (detection.Repository is not { } repository)
        {
            return detection.TestSuite is null
                ? []
                :
                [
                    new DetectTool(context),
                    new RestoreTool(repository: null, context: context),
                    new JobStatusTool(context.Runner),
                    new JobResultTool(context.Runner),
                ];
        }

        if (repository.SupportedProjects.Count == 0)
        {
            return [new DetectTool(context)];
        }

        return
        [
            new DetectTool(context),
            new RestoreTool(repository: repository, context: context),
            new TestTool(repository: repository, context: context),
            new LintTool(repository: repository, context: context),
            new CoverageTool(repository: repository, context: context),
            new JobStatusTool(context.Runner),
            new JobResultTool(context.Runner),
        ];
    }
}
