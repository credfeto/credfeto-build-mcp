using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration.Exceptions;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Core.Configuration;

[DebuggerDisplay("RepositoryRoot={RepositoryRoot} ({RepositoryRootSource}), LogDirectory={LogDirectory}")]
public sealed record BuildServerEnvironment(
    string RepositoryRoot,
    RepositoryRootSource RepositoryRootSource,
    string LogDirectory
)
{
    public const string REPOSITORY_ROOT_VARIABLE = "BUILD_MCP_REPO_ROOT";
    public const string LOG_DIRECTORY_VARIABLE = "BUILD_MCP_LOG_DIR";
    public const string STATE_HOME_VARIABLE = "XDG_STATE_HOME";
    public const string HOME_VARIABLE = "HOME";
    public const string DEFAULT_REPOSITORY_ROOT = "/workspace/repo";

    private const string APPLICATION_FOLDER = "credfeto-build-mcp";
    private const string LOGS_FOLDER = "logs";
    private const string PRIVATE_FOLDER_DESCRIPTION = "a new private folder in the temporary directory";

    public static ValueTask<BuildServerEnvironment> FromProcessAsync(CancellationToken cancellationToken)
    {
        return ResolveAsync(
            getVariable: ProcessVariable,
            createPrivateFolder: CreatePrivateTemporaryFolder,
            defaultRepositoryRoot: DEFAULT_REPOSITORY_ROOT,
            findGitRoot: FindWorkingDirectoryGitRootAsync,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask<BuildServerEnvironment> ResolveAsync(
        Func<string, string?> getVariable,
        Func<string> createPrivateFolder,
        string defaultRepositoryRoot,
        Func<CancellationToken, ValueTask<string?>> findGitRoot,
        CancellationToken cancellationToken
    )
    {
        (string repositoryRoot, RepositoryRootSource source) = await ResolveRepositoryRootAsync(
            value: getVariable(REPOSITORY_ROOT_VARIABLE),
            defaultRepositoryRoot: defaultRepositoryRoot,
            findGitRoot: findGitRoot,
            cancellationToken: cancellationToken
        );

        string logDirectory = ResolveLogDirectory(
            value: getVariable(LOG_DIRECTORY_VARIABLE),
            homeLogDirectory: HomeLogDirectory(getVariable),
            createPrivateFolder: createPrivateFolder
        );

        return new(RepositoryRoot: repositoryRoot, RepositoryRootSource: source, LogDirectory: logDirectory);
    }

    public string DescribeRepositoryRoot()
    {
        string reason = this.RepositoryRootSource switch
        {
            RepositoryRootSource.Default => "the default; " + REPOSITORY_ROOT_VARIABLE + " is not set",
            RepositoryRootSource.GitRoot => "the git root of the current directory; "
                + REPOSITORY_ROOT_VARIABLE
                + " is not set and the default folder does not exist",
            _ => "from " + REPOSITORY_ROOT_VARIABLE,
        };

        return "repository root " + this.RepositoryRoot + " (" + reason + ")";
    }

    private static async ValueTask<(string Root, RepositoryRootSource Source)> ResolveRepositoryRootAsync(
        string? value,
        string defaultRepositoryRoot,
        Func<CancellationToken, ValueTask<string?>> findGitRoot,
        CancellationToken cancellationToken
    )
    {
        // Only an unset variable gets a fallback: a value that is set but not absolute (relative, empty or whitespace)
        // is a misconfiguration, and silently swapping it for the default would hide it
        if (value is not null)
        {
            return (
                RequireAbsolute(
                    name: REPOSITORY_ROOT_VARIABLE,
                    value: value,
                    defaultDescription: defaultRepositoryRoot
                        + " (or, when that does not exist, the git root of the current directory)"
                ),
                RepositoryRootSource.Variable
            );
        }

        // The container mounts the repository at the default; git is only asked when that is missing, so the
        // container start runs no process
        if (Directory.Exists(defaultRepositoryRoot))
        {
            return (Normalise(defaultRepositoryRoot), RepositoryRootSource.Default);
        }

        string? gitRoot = await findGitRoot(cancellationToken);

        return IsAbsolute(gitRoot)
            ? (Normalise(gitRoot), RepositoryRootSource.GitRoot)
            : throw new InvalidBuildConfigurationException(
                REPOSITORY_ROOT_VARIABLE
                    + " is not set, "
                    + defaultRepositoryRoot
                    + " does not exist and the current directory is not in a git checkout: set "
                    + REPOSITORY_ROOT_VARIABLE
                    + " to the repository's absolute path, or start the server inside the repository"
            );
    }

    private static async ValueTask<string?> FindWorkingDirectoryGitRootAsync(CancellationToken cancellationToken)
    {
        string? directory = CurrentDirectory();

        return directory is null
            ? null
            : await GitTopLevel.FindAsync(directory: directory, cancellationToken: cancellationToken);
    }

    private static string? CurrentDirectory()
    {
        try
        {
            return Environment.CurrentDirectory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The directory the server started in has been removed or can't be read, so it is in no checkout
            return null;
        }
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
