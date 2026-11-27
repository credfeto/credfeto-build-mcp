using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tools;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Node.Server.Tests.Helpers;

internal static class TestTools
{
    public const string NPM = "/usr/local/bin/npm";

    public const long FOUR_GIB = 4L * 1024 * 1024 * 1024;

    public static readonly NpmLocator NoNpm = new(static _ => null);

    public static readonly NpmLocator FakeNpm = new(static _ => NPM);

    // Eight processors in a 4 GiB container: coverage runs 2 workers and a plain test run 4
    public static readonly MachineResources FourGigabyteHost = new(
        memory: FixedMemorySource.CgroupV2(FOUR_GIB),
        processorCount: 8
    );

    public static ToolContext Context(NodeDetection detection)
    {
        return Context(detection: detection, runner: new RecordingJobRunner(), npm: FakeNpm);
    }

    public static ToolContext Context(NodeDetection detection, IJobRunner runner, NpmLocator npm)
    {
        return Context(detection: detection, runner: runner, npm: npm, resources: FourGigabyteHost);
    }

    public static ToolContext Context(
        NodeDetection detection,
        IJobRunner runner,
        NpmLocator npm,
        MachineResources resources
    )
    {
        return new(detection: new FixedDetectionSource(detection), runner: runner, npm: npm, resources: resources);
    }

    public static IReadOnlyList<McpServerTool> Catalog(NodeDetection detection, IJobRunner runner, NpmLocator npm)
    {
        return Catalog(detection: detection, runner: runner, npm: npm, resources: FourGigabyteHost);
    }

    public static IReadOnlyList<McpServerTool> Catalog(
        NodeDetection detection,
        IJobRunner runner,
        NpmLocator npm,
        MachineResources resources
    )
    {
        return NodeToolCatalog.Create(
            detection: detection,
            context: Context(detection: detection, runner: runner, npm: npm, resources: resources)
        );
    }

    public static BuildTool Find(IEnumerable<McpServerTool> tools, string name)
    {
        return (BuildTool)
            tools.Single(candidate => StringComparer.Ordinal.Equals(x: candidate.ProtocolTool.Name, y: name));
    }

    public static NodeDetection Detected(NodeRepository repository)
    {
        return new(
            RepositoryRoot: repository.RepositoryRoot,
            Repository: repository,
            Reason: "npm packages found",
            OverridesError: null,
            WatchedDirectories: NodeDetector.BaseWatchedDirectories
        );
    }
}
