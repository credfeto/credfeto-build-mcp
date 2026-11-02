using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

internal static class TestContexts
{
    public static readonly BuildtestLocator NoBuildtest = new(static _ => null);

    public static ToolContext Create(IJobRunner runner, DotnetDetection detection)
    {
        return Create(runner: runner, detection: detection, buildtest: NoBuildtest);
    }

    public static ToolContext Create(IJobRunner runner, DotnetDetection detection, BuildtestLocator buildtest)
    {
        return new(runner: runner, detection: new FixedDetectionSource(detection), buildtest: buildtest);
    }

    public static IReadOnlyList<McpServerTool> Tools(IJobRunner runner, DotnetDetection detection)
    {
        return DotnetToolCatalog.Create(detection: detection, context: Create(runner: runner, detection: detection));
    }

    public static BuildTool Find(IEnumerable<McpServerTool> tools, string name)
    {
        return (BuildTool)tools.Single(candidate => StringComparer.Ordinal.Equals(x: candidate.ProtocolTool.Name, y: name));
    }
}
