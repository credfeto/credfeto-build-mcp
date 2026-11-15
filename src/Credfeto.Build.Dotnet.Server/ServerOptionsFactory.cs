using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Workspace;
using Credfeto.Build.Server;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerOptionsFactory
{
    public const string SERVER_NAME = "credfeto-build-dotnet";

    public static McpServerOptions Create(DotnetWorkspace workspace)
    {
        return McpServerOptionsFactory.Create(
            name: SERVER_NAME,
            versionSource: typeof(ServerOptionsFactory).Assembly,
            instructions: Instructions(workspace.Current),
            tools: workspace.Tools
        );
    }

    private static string Instructions(DotnetDetection detection)
    {
        return detection.Repository is null
            ? "No .NET solution was found in "
                + detection.RepositoryRoot
                + " ("
                + detection.Reason
                + "), so this server offers no tools for now. It watches the repository and offers its tools, with notifications/tools/list_changed, once a solution appears."
            : "Builds, tests, checks and measures coverage for the .NET solution in "
                + detection.RepositoryRoot
                + ". Every tool except detect, package_versions, job_status and job_result starts a job and returns its id at once: poll job_status until the state is no longer running, then read job_result. One job runs at a time. The tool list follows the repository: when projects are added or removed the server sends notifications/tools/list_changed.";
    }
}
