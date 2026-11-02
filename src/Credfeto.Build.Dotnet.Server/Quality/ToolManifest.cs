using System.IO;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Quality;

/// <summary>The repository's .NET tool manifest, which every quality tool runs its tool from.</summary>
public static class ToolManifest
{
    public static bool Exists(string repositoryRoot)
    {
        return File.Exists(Path.Combine(path1: repositoryRoot, path2: ".config", path3: "dotnet-tools.json"))
            || File.Exists(Path.Combine(path1: repositoryRoot, path2: "dotnet-tools.json"));
    }

    public static void Require(DotnetRepository repository, string tool)
    {
        if (!Exists(repository.RepositoryRoot))
        {
            throw new ToolUnavailableException(
                "The repository has no .NET tool manifest (.config/dotnet-tools.json or dotnet-tools.json), so dotnet " + tool + " cannot be run from it"
            );
        }
    }
}
