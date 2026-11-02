using System.Collections.Generic;
using System.Reflection;
using Credfeto.Build.Dotnet.Server.Detection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server;

public static class ServerOptionsFactory
{
    public const string SERVER_NAME = "credfeto-build-dotnet";

    public static McpServerOptions Create(DotnetDetection detection, IReadOnlyList<McpServerTool> tools)
    {
        // Always set, even when empty, so tools/list answers with no tools rather than an unknown-method error
        McpServerPrimitiveCollection<McpServerTool> collection = [];

        foreach (McpServerTool tool in tools)
        {
            collection.Add(tool);
        }

        return new()
        {
            ServerInfo = new() { Name = SERVER_NAME, Version = Version() },
            ServerInstructions = Instructions(detection),
            ToolCollection = collection,
        };
    }

    private static string Instructions(DotnetDetection detection)
    {
        return detection.Repository is null
            ? "No .NET solution was found in " + detection.RepositoryRoot + " (" + detection.Reason + "), so this server offers no tools."
            : "Builds, tests and measures coverage for the .NET solution in " + detection.RepositoryRoot
                + ". restore, build, test and coverage start a job and return its id at once: poll job_status until the state is no longer running, then read job_result. One job runs at a time.";
    }

    private static string Version()
    {
        return typeof(ServerOptionsFactory).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
    }
}
