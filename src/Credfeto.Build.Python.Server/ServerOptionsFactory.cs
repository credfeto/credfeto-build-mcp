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
            return "Restores, tests and lints the Python projects in "
                + detection.RepositoryRoot
                + " (pip with a per-project .venv, or uv when uv.lock exists), instead of raw pip, uv, pytest, flake8 or pylint commands. Every tool except detect, job_status and job_result starts a job and returns its id at once: call job_result, which waits for the job to finish, and call it again only while its state is still running. job_status is a single look at progress; never call it in a loop. One job runs at a time. Read the notes a tool returns: they say when pytest or coverage was installed because the project does not declare them. The tool list follows the repository: when projects change the server sends notifications/tools/list_changed.";
        }

        if (detection.TestSuite is not null)
        {
            return "There are Python test files in "
                + detection.RepositoryRoot
                + " but no pyproject.toml or requirements*.txt declaring what they need. Call restore: it creates a minimal pyproject.toml (or requirements-dev.txt) at the repository root and installs from it; commit the file and add anything missing. The server then offers its other tools, with notifications/tools/list_changed.";
        }

        return "No supported Python project was found in "
            + detection.RepositoryRoot
            + " ("
            + detection.Reason
            + "), so this server offers no tools for now. It watches the repository and offers its tools, with notifications/tools/list_changed, once a pyproject.toml with a [project] table or a requirements*.txt appears at the repository root or in src/.";
    }
}
