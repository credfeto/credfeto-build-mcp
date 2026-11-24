using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    public const string VERSION_STEP = "python version";

    public static readonly PythonLocator FakeTools = new(static name => "/usr/bin/" + name);

    public static ToolContext Context(PythonDetection detection)
    {
        return Context(detection: detection, runner: new RecordingJobRunner(), python: FakePython);
    }

    public static ToolContext Context(PythonDetection detection, IJobRunner runner, PythonLocator python)
    {
        return new(
            detection: new FixedDetectionSource(detection),
            runner: runner,
            commands: VersionCommands(),
            python: python
        );
    }

    // A command runner that answers the interpreter version probe with 3.13.1
    public static FakeCommandRunner VersionCommands()
    {
        return new FakeCommandRunner().Answer(stepName: VERSION_STEP, standardOutput: "3.13.1\n");
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

    public static Dictionary<string, JsonElement> Arguments(params (string Name, string Value)[] values)
    {
        Dictionary<string, JsonElement> arguments = [with(StringComparer.Ordinal)];

        foreach ((string name, string value) in values)
        {
            arguments[name] = JsonSerializer.SerializeToElement(
                value: value,
                jsonTypeInfo: TestJsonContext.Default.String
            );
        }

        return arguments;
    }
}
