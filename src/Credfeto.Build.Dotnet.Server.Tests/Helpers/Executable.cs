using System;
using System.IO;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

internal static class Executable
{
    public static void Mark(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path: path, mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
