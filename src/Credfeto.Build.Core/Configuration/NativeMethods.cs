using System.Runtime.InteropServices;

namespace Credfeto.Build.Core.Configuration;

internal static class NativeMethods
{
    [DllImport(dllName: "libc", EntryPoint = "geteuid", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static extern uint GetEffectiveUserId();
}
