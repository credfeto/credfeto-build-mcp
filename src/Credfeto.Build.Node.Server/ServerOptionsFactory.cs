using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Workspace;
using Credfeto.Build.Server;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Node.Server;

public static class ServerOptionsFactory
{
    public const string SERVER_NAME = "credfeto-build-node";

    public static McpServerOptions Create(NodeWorkspace workspace)
    {
        return McpServerOptionsFactory.Create(
            name: SERVER_NAME,
            versionSource: typeof(ServerOptionsFactory).Assembly,
            instructions: Instructions(workspace.Current),
            tools: workspace.Tools
        );
    }

    private static string Instructions(NodeDetection detection)
    {
        return detection.Repository is null
            ? "No supported Node repository was found in "
                + detection.RepositoryRoot
                + " ("
                + detection.Reason
                + "), so this server offers no tools for now. It watches the repository and offers its tools, with notifications/tools/list_changed, once an npm package.json with its lock file appears."
            : "Inspects the npm packages in "
                + detection.RepositoryRoot
                + ". The tool list follows the repository: when packages are added or removed the server sends notifications/tools/list_changed.";
    }
}
