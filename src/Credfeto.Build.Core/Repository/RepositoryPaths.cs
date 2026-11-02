using System;
using System.IO;

namespace Credfeto.Build.Core.Repository;

public static class RepositoryPaths
{
    public static bool IsWithin(string root, string path)
    {
        string relative = Path.GetRelativePath(relativeTo: root, path: path);

        return !Path.IsPathRooted(relative)
            && !StringComparer.Ordinal.Equals(x: relative, y: "..")
            && !relative.StartsWith(value: ".." + Path.DirectorySeparatorChar, comparisonType: StringComparison.Ordinal);
    }

    public static string ToForwardSlashes(string path)
    {
        return path.Replace(oldChar: '\\', newChar: '/');
    }

    public static string RelativeTo(string root, string path)
    {
        return ToForwardSlashes(Path.GetRelativePath(relativeTo: root, path: path));
    }

    public static string WithTrailingSeparator(string directory)
    {
        return Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
    }
}
