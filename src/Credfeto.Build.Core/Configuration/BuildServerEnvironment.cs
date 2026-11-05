using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Credfeto.Build.Core.Configuration;

[DebuggerDisplay("RepositoryRoot={RepositoryRoot}, LogDirectory={LogDirectory}")]
public sealed record BuildServerEnvironment(string RepositoryRoot, string LogDirectory)
{
    public const string REPOSITORY_ROOT_VARIABLE = "BUILD_MCP_REPO_ROOT";
    public const string LOG_DIRECTORY_VARIABLE = "BUILD_MCP_LOG_DIR";
    public const string DEFAULT_REPOSITORY_ROOT = "/workspace/repo";

    private const string DEFAULT_LOG_FOLDER = "credfeto-build-mcp";

    public static BuildServerEnvironment FromProcess()
    {
        return Resolve(Environment.GetEnvironmentVariable);
    }

    public static BuildServerEnvironment Resolve(Func<string, string?> getVariable)
    {
        string repositoryRoot = AbsoluteOrDefault(
            value: getVariable(REPOSITORY_ROOT_VARIABLE),
            fallback: DEFAULT_REPOSITORY_ROOT
        );
        string logDirectory = AbsoluteOrDefault(
            value: getVariable(LOG_DIRECTORY_VARIABLE),
            fallback: Path.Combine(path1: Path.GetTempPath(), path2: DEFAULT_LOG_FOLDER)
        );

        return new(RepositoryRoot: repositoryRoot, LogDirectory: logDirectory);
    }

    public static IReadOnlyDictionary<string, string> SnapshotVariables()
    {
        Dictionary<string, string> variables = [with(StringComparer.Ordinal)];

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry is { Key: string name, Value: string value })
            {
                variables[name] = value;
            }
        }

        return variables;
    }

    private static string AbsoluteOrDefault(string? value, string fallback)
    {
        // Only an absolute path is accepted, so a relative value can never resolve against whatever directory the
        // server happens to start in
        string chosen = !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value) ? value : fallback;

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(chosen));
    }
}
