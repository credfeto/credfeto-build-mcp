using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    private const string PRIVATE_FOLDER_DESCRIPTION = "a new private folder in the temporary directory";

    public static BuildServerEnvironment FromProcess()
    {
        return Resolve(getVariable: ProcessVariable, createPrivateFolder: CreatePrivateTemporaryFolder);
    }

    public static BuildServerEnvironment Resolve(Func<string, string?> getVariable, Func<string> createPrivateFolder)
    {
        // Only an unset variable gets the default: a value that is set but not absolute (relative, empty or whitespace)
        // is a misconfiguration, and silently swapping it for the default would hide it
        string? rootValue = getVariable(REPOSITORY_ROOT_VARIABLE);
        string repositoryRoot = rootValue is null
            ? Normalise(DEFAULT_REPOSITORY_ROOT)
            : RequireAbsolute(
                name: REPOSITORY_ROOT_VARIABLE,
                value: rootValue,
                defaultDescription: DEFAULT_REPOSITORY_ROOT
            );

        string logDirectory = ResolveLogDirectory(
            value: getVariable(LOG_DIRECTORY_VARIABLE),
            homeLogDirectory: HomeLogDirectory(getVariable),
            createPrivateFolder: createPrivateFolder
        );

        return new(RepositoryRoot: repositoryRoot, LogDirectory: logDirectory);
    }

    private static string ResolveLogDirectory(string? value, string? homeLogDirectory, Func<string> createPrivateFolder)
    {
        if (homeLogDirectory is null)
        {
            // The private folder is only created when it is used, never just to name it in an error
            return value is null
                ? Normalise(PrivateFolder(createPrivateFolder))
                : RequireAbsolute(
                    name: LOG_DIRECTORY_VARIABLE,
                    value: value,
                    defaultDescription: PRIVATE_FOLDER_DESCRIPTION
                );
        }

        string fallback = Normalise(homeLogDirectory);

        return value is null
            ? fallback
            : RequireAbsolute(name: LOG_DIRECTORY_VARIABLE, value: value, defaultDescription: fallback);
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

    public static string CreatePrivateTemporaryFolder()
    {
        // The temporary directory is shared by every user, so a fixed name there could be created first, or replaced
        // by a symlink, by another account. This makes a new folder with a random name in one step (mkdtemp): owner
        // only, never a link, and never one that already existed
        return Directory.CreateTempSubdirectory(APPLICATION_FOLDER + "-").FullName;
    }

    private static string PrivateFolder(Func<string> createPrivateFolder)
    {
        try
        {
            return createPrivateFolder();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidBuildConfigurationException(
                "There is no home directory for the job logs, and a private folder could not be created in the temporary directory: set "
                    + LOG_DIRECTORY_VARIABLE
                    + " to an absolute path",
                innerException: exception
            );
        }
    }

    private static string? HomeLogDirectory(Func<string, string?> getVariable)
    {
        // Each user gets their own folder, so one user's server can neither block nor read another user's logs
        string? stateHome = getVariable(STATE_HOME_VARIABLE);

        if (IsAbsolute(stateHome))
        {
            return Path.Combine(path1: stateHome, path2: APPLICATION_FOLDER, path3: LOGS_FOLDER);
        }

        string? home = getVariable(HOME_VARIABLE);

        return IsAbsolute(home) ? Path.Combine(home, ".local", "state", APPLICATION_FOLDER, LOGS_FOLDER) : null;
    }

    private static string? ProcessVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value) && StringComparer.Ordinal.Equals(x: name, y: HOME_VARIABLE)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : value;
    }

    private static bool IsAbsolute([NotNullWhen(true)] string? value)
    {
        // Only an absolute path is accepted, so a relative value can never resolve against whatever directory the
        // server happens to start in
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value);
    }

    private static string RequireAbsolute(string name, string value, string defaultDescription)
    {
        return IsAbsolute(value)
            ? Normalise(value)
            : throw new InvalidBuildConfigurationException(
                name
                    + " is set to \""
                    + value
                    + "\", which is not an absolute path: set it to an absolute path, or unset it to use "
                    + defaultDescription
            );
    }

    private static string Normalise(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
