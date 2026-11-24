using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tools;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Python.Server.Tests.Helpers;

internal static class TestTools
{
    public const string PYTHON = "/usr/bin/python3";

    public static readonly PythonLocator NoPython = new(static _ => null);

    public static readonly PythonLocator FakePython = new(static _ => PYTHON);

    public static ToolContext Context(PythonDetection detection)
    {
        return Context(detection: detection, runner: new RecordingJobRunner(), python: FakePython);
    }

    public static ToolContext Context(PythonDetection detection, IJobRunner runner, PythonLocator python)
    {
        return new(detection: new FixedDetectionSource(detection), runner: runner, python: python);
    }

    public static IReadOnlyList<McpServerTool> Catalog(PythonDetection detection)
    {
        return PythonToolCatalog.Create(detection: detection, context: Context(detection));
    }

    public static BuildTool Find(IEnumerable<McpServerTool> tools, string name)
    {
        return (BuildTool)
            tools.Single(candidate => StringComparer.Ordinal.Equals(x: candidate.ProtocolTool.Name, y: name));
    }

    public static PythonDetection Detected(PythonRepository repository)
    {
        return new(
            RepositoryRoot: repository.RepositoryRoot,
            Repository: repository,
            Reason: "Python projects found",
            OverridesError: null,
            WatchedDirectories: PythonDetector.BaseWatchedDirectories
        );
    }

    public static PythonDetection NotDetected(string repositoryRoot)
    {
        return new(
            RepositoryRoot: repositoryRoot,
            Repository: null,
            Reason: "No pyproject.toml with a [project] table or requirements*.txt at the repository root or in src/",
            OverridesError: null,
            WatchedDirectories: PythonDetector.BaseWatchedDirectories
        );
    }

    public static string Text(CallToolResult result)
    {
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(static block => block.Text));
    }
}
