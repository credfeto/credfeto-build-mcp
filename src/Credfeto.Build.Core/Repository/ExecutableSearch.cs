using System;
using System.IO;
using System.Linq;

namespace Credfeto.Build.Core.Repository;

/// <summary>Finds an executable by name on a <c>PATH</c>-style list of absolute directories.</summary>
public static class ExecutableSearch
{
    private const UnixFileMode EXECUTABLE = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    public static string? Find(string name, string? searchPath)
    {
        return (searchPath ?? string.Empty)
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
            .Where(Path.IsPathFullyQualified)
            .Select(directory => Path.Combine(path1: directory, path2: name))
            .FirstOrDefault(IsExecutable);
    }

    public static bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            return OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & EXECUTABLE) != UnixFileMode.None;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // For example a symbolic link into a directory this user cannot read
            return false;
        }
    }
}
