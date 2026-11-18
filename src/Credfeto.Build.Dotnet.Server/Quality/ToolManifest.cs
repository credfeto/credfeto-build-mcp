using System;
using System.IO;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Quality;

public static class ToolManifest
{
    private const string FILE_NAME = "dotnet-tools.json";

    public static string? Find(string repositoryRoot)
    {
        return Find(repositoryRoot: repositoryRoot, fileExists: File.Exists);
    }

    // Searches the way dotnet tool run does: the start directory, then each parent up to the filesystem root,
    // .config/dotnet-tools.json before dotnet-tools.json in each, stopping at the first manifest found
    public static string? Find(string repositoryRoot, Func<string, bool> fileExists)
    {
        for (
            string? directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
            directory is not null;
            directory = Path.GetDirectoryName(directory)
        )
        {
            string? manifest = FindIn(directory: directory, fileExists: fileExists);

            if (manifest is not null)
            {
                return manifest;
            }
        }

        return null;
    }

    public static void Require(DotnetRepository repository, string tool)
    {
        if (Find(repository.RepositoryRoot) is null)
        {
            string root = Path.GetFullPath(repository.RepositoryRoot);

            throw new ToolUnavailableException(
                "No .NET tool manifest (.config/dotnet-tools.json or dotnet-tools.json) was found in the repository root "
                    + root
                    + " or any parent directory up to "
                    + Path.GetPathRoot(root)
                    + ", so dotnet "
                    + tool
                    + " cannot be run from it"
            );
        }
    }

    private static string? FindIn(string directory, Func<string, bool> fileExists)
    {
        string inConfig = Path.Combine(path1: directory, path2: ".config", path3: FILE_NAME);

        if (fileExists(inConfig))
        {
            return inConfig;
        }

        string inDirectory = Path.Combine(path1: directory, path2: FILE_NAME);

        return fileExists(inDirectory) ? inDirectory : null;
    }
}
