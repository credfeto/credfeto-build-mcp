using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Workspace;
using Credfeto.Build.Server;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Python.Server;

public static class ServerOptionsFactory
{
    public const string SERVER_NAME = "credfeto-build-python";

    public static McpServerOptions Create(PythonWorkspace workspace)
    {
        return McpServerOptionsFactory.Create(
            name: SERVER_NAME,
            versionSource: typeof(ServerOptionsFactory).Assembly,
            instructions: Instructions(workspace.Current),
            tools: workspace.Tools
        );
    }

    private static string Instructions(PythonDetection detection)
    {
        if (detection.Repository is not null)
        {
            return "Detects the Python projects in "
                + detection.RepositoryRoot
                + " (pip with a virtual environment, or uv when uv.lock exists), instead of raw pip, uv, pytest or coverage commands. The repository is detected afresh on every call. The tool list follows the repository: when projects change the server sends notifications/tools/list_changed.";
        }

        if (detection.TestSuite is not null)
        {
            return "There are Python test files in "
                + detection.RepositoryRoot
                + " but no pyproject.toml or requirements*.txt declaring what they need, so this server offers only detect for now, which lists where the tests are. It watches the repository and offers its tools, with notifications/tools/list_changed, once a manifest appears.";
        }

        return "No supported Python project was found in "
            + detection.RepositoryRoot
            + " ("
            + detection.Reason
            + "), so this server offers no tools for now. It watches the repository and offers its tools, with notifications/tools/list_changed, once a pyproject.toml with a [project] table or a requirements*.txt appears at the repository root or in src/.";
    }
}
