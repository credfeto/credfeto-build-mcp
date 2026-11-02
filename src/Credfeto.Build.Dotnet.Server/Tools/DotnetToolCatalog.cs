using System.Collections.Generic;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Tools;

public static class DotnetToolCatalog
{
    /// <summary>
    ///     The tools for the detected repository: none at all when no .NET solution was found, and test and
    ///     coverage only when there is at least one unit test project to offer in their enums.
    /// </summary>
    public static IReadOnlyList<McpServerTool> Create(DotnetDetection detection, IJobRunner runner)
    {
        if (detection.Repository is not { } repository)
        {
            return [];
        }

        List<McpServerTool> tools =
        [
            new DetectTool(detection: detection, repository: repository),
            new RestoreTool(repository: repository, runner: runner),
            new BuildSolutionTool(repository: repository, runner: runner),
        ];

        if (repository.UnitTestProjects.Count != 0)
        {
            tools.Add(new TestTool(repository: repository, runner: runner));
            tools.Add(new CoverageTool(repository: repository, runner: runner));
        }

        tools.Add(new JobStatusTool(runner));
        tools.Add(new JobResultTool(runner));

        return tools;
    }
}
