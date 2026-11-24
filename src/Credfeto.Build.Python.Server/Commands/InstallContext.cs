using System.Diagnostics;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Commands;

// Everything a project's steps need, worked out when the tool is called: the interpreter and its version, the venv,
// whether it exists, the stamp an install writes, and uv's path for a uv project
[DebuggerDisplay("Project={Project.Name}, Version={Version.Text}")]
public sealed record InstallContext(
    string RepositoryRoot,
    PythonProject Project,
    string Interpreter,
    PythonVersion Version,
    VirtualEnvironment Environment,
    string Stamp
)
{
    // The venv was completed by an earlier install (it has its interpreter and a stamp); one that is not, such as a
    // venv left half made by a cancelled job, is created again from scratch
    public bool VenvExists { get; init; }

    public bool IsCurrent { get; init; }

    public string? Uv { get; init; }
}
