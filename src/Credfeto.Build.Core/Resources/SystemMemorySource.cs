using System;
using System.IO;

namespace Credfeto.Build.Core.Resources;

public sealed class SystemMemorySource : IMemorySource
{
    public string? ReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A host without cgroups or /proc (macOS, Windows, a locked-down container) has no limit to read
            return null;
        }
    }
}
