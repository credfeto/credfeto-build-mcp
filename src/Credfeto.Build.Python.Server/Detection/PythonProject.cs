using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Detection;

// A project: its name ([project].name, or its directory), its directory relative to the repository root ("." for the
// root), how its dependencies are installed and what its manifests declare. Problem says why an Unsupported project
// cannot be used
[DebuggerDisplay("Name={Name}, RelativeDirectory={RelativeDirectory}, Installer={Installer}")]
public sealed record PythonProject(string Name, string RelativeDirectory, PythonInstaller Installer)
{
    public const string ROOT_DIRECTORY = ".";

    public string? Problem { get; init; }

    // The project's manifest and lock files, relative to the project's directory
    public IReadOnlyList<string> Manifests { get; init; } = [];

    // The requirements*.txt files in the project's directory, in name order
    public IReadOnlyList<string> RequirementFiles { get; init; } = [];

    // pyproject.toml has a [project] table
    public bool HasProjectTable { get; init; }

    public string? RequiresPython { get; init; }

    // [project].dependencies
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    // The optional-dependency extras and dependency groups named dev, test or tests: what a test run installs
    public IReadOnlyList<string> TestExtras { get; init; } = [];

    public IReadOnlyList<string> TestGroups { get; init; } = [];

    // The requirements the test extras and groups add, included groups expanded
    public IReadOnlyList<string> TestRequirements { get; init; } = [];

    public bool DeclaresPytest { get; init; }

    public bool DeclaresCoverage { get; init; }

    public bool IsSupported => this.Installer != PythonInstaller.Unsupported;
}
