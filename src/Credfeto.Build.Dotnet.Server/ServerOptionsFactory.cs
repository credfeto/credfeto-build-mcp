using System.Reflection;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerOptionsFactory
{
    public const string SERVER_NAME = "credfeto-build-dotnet";

    public static McpServerOptions Create(DotnetWorkspace workspace)
    {
        return new()
        {
            ServerInfo = new() { Name = SERVER_NAME, Version = Version() },
            ServerInstructions = Instructions(workspace.Current),

            // The live collection, always set even when empty, so tools/list answers with no tools rather than an
            // unknown-method error, and a change to it is sent to the client as notifications/tools/list_changed
            Capabilities = new() { Tools = new() { ListChanged = true } },
            ToolCollection = workspace.Tools,
        };
    }

    private static string Instructions(DotnetDetection detection)
    {
        return detection.Repository is null
            ? "No .NET solution was found in " + detection.RepositoryRoot + " (" + detection.Reason + "), so this server offers no tools for now. It watches the repository and offers its tools, with notifications/tools/list_changed, once a solution appears."
            : "Builds, tests, checks and measures coverage for the .NET solution in " + detection.RepositoryRoot
                + ". Every tool except detect, job_status and job_result starts a job and returns its id at once: poll job_status until the state is no longer running, then read job_result. One job runs at a time. The tool list follows the repository: when projects are added or removed the server sends notifications/tools/list_changed.";
    }

    private static string Version()
    {
        return typeof(ServerOptionsFactory).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
    }
}
