using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Dotnet.Server.Tools;

public static class ToolNames
{
    public const string DETECT = "detect";
    public const string PACKAGE_VERSIONS = "package_versions";
    public const string RESTORE = "restore";
    public const string BUILD = "build";
    public const string TEST = "test";
    public const string COVERAGE = "coverage";
    public const string BUILDCHECK = "buildcheck";
    public const string CLEANUP = "cleanup";
    public const string BUILDTEST = "buildtest";
    public const string JOB_STATUS = JobToolNames.JOB_STATUS;
    public const string JOB_RESULT = JobToolNames.JOB_RESULT;
}
