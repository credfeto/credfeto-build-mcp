using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Credfeto.Build.Core.Configuration.Exceptions;

namespace Credfeto.Build.Core.Configuration;

[DebuggerDisplay("RepositoryRoot={RepositoryRoot}, LogDirectory={LogDirectory}")]
public sealed record BuildServerEnvironment(string RepositoryRoot, string LogDirectory)
{
    public const string REPOSITORY_ROOT_VARIABLE = "BUILD_MCP_REPO_ROOT";
    public const string LOG_DIRECTORY_VARIABLE = "BUILD_MCP_LOG_DIR";
    public const string STATE_HOME_VARIABLE = "XDG_STATE_HOME";
    public const string HOME_VARIABLE = "HOME";
    public const string DEFAULT_REPOSITORY_ROOT = "/workspace/repo";

    private const string APPLICATION_FOLDER = "credfeto-build-mcp";
    private const string LOGS_FOLDER = "logs";

    public static BuildServerEnvironment FromProcess()
    {
        return Resolve(getVariable: ProcessVariable, currentUser: CurrentUser);
    }

    public static BuildServerEnvironment Resolve(Func<string, string?> getVariable, Func<string> currentUser)
    {
        string repositoryRoot = AbsoluteOrDefault(
            name: REPOSITORY_ROOT_VARIABLE,
            value: getVariable(REPOSITORY_ROOT_VARIABLE),
            fallback: DEFAULT_REPOSITORY_ROOT
        );
        string logDirectory = AbsoluteOrDefault(
            name: LOG_DIRECTORY_VARIABLE,
            value: getVariable(LOG_DIRECTORY_VARIABLE),
            fallback: DefaultLogDirectory(getVariable: getVariable, currentUser: currentUser)
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

    private static string DefaultLogDirectory(Func<string, string?> getVariable, Func<string> currentUser)
    {
        // Each user gets their own folder, so one user's server can neither block nor read another user's logs
        string? stateHome = getVariable(STATE_HOME_VARIABLE);

        if (IsAbsolute(stateHome))
        {
            return Path.Combine(path1: stateHome, path2: APPLICATION_FOLDER, path3: LOGS_FOLDER);
        }

        string? home = getVariable(HOME_VARIABLE);

        if (IsAbsolute(home))
        {
            return Path.Combine(home, ".local", "state", APPLICATION_FOLDER, LOGS_FOLDER);
        }

        return Path.Combine(path1: Path.GetTempPath(), path2: APPLICATION_FOLDER + "-" + currentUser());
    }

    private static string? ProcessVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value) && StringComparer.Ordinal.Equals(x: name, y: HOME_VARIABLE)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : value;
    }

    public static string CurrentUser()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.UserName;
        }

        try
        {
            return NativeMethods.GetEffectiveUserId().ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // The name still separates users when the C library cannot be probed under its generic name
            return Environment.UserName;
        }
    }

    private static bool IsAbsolute([NotNullWhen(true)] string? value)
    {
        // Only an absolute path is accepted, so a relative value can never resolve against whatever directory the
        // server happens to start in
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value);
    }

    private static string AbsoluteOrDefault(string name, string? value, string fallback)
    {
        // Only an unset variable gets the default: a value that is set but not absolute (relative, empty or
        // whitespace) is a misconfiguration, and silently swapping it for the default would hide it
        if (value is null)
        {
            return Normalise(fallback);
        }

        return IsAbsolute(value)
            ? Normalise(value)
            : throw new InvalidBuildConfigurationException(
                name
                    + " is set to \""
                    + value
                    + "\", which is not an absolute path: set it to an absolute path, or unset it to use "
                    + Normalise(fallback)
            );
    }

    private static string Normalise(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
