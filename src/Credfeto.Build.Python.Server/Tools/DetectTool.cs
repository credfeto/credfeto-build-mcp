using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Json;
using Credfeto.Build.Python.Server.Models;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Tools;

public sealed class DetectTool : BuildTool
{
    public const string TEST_SUITE_NOTE =
        "No pyproject.toml or requirements*.txt declares what these tests need. Call restore: it creates a minimal pyproject.toml (or requirements-dev.txt) at the repository root declaring pytest and coverage and these directories, which you then commit and complete.";

    private readonly ToolContext _context;

    public DetectTool(ToolContext context)
        : base(
            name: ToolNames.DETECT,
            title: "Detect Python",
            description: "Lists the Python projects (a pyproject.toml with a [project] table or a requirements*.txt at the repository root, in src/ or in a python.projects directory) with the installer each uses (pip with a virtual environment, or uv when uv.lock exists; poetry, pipenv and pdm are reported as unsupported), its manifests, requires-python, the dev/test/tests extras and dependency groups, and whether pytest and coverage are declared, plus the python3 found on PATH. With no manifest at all it lists the directories holding Python test files instead. The repository is detected afresh on every call",
            schema: [],
            readOnly: true
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        PythonDetection detection = await this._context.Detection.RefreshAsync(cancellationToken);

        if (detection.Repository is null && detection.TestSuite is null)
        {
            throw new ToolUnavailableException("There is no Python project any more (" + detection.Reason + ")");
        }

        DetectResponse response = new(
            RepositoryRoot: detection.RepositoryRoot,
            Interpreter: this._context.Python.Find(),
            Projects: detection.Repository is { } repository ? [.. repository.Projects.Select(Describe)] : [],
            TestSuiteWithoutManifest: detection.TestSuite is { } testSuite
                ? new(Directories: testSuite.TestDirectories, Note: TEST_SUITE_NOTE)
                : null,
            TimeoutMinutes: detection.Repository is { } timed ? (int)timed.Timeout.TotalMinutes : null,
            Reason: detection.Reason,
            OverridesError: detection.OverridesError
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: PythonJsonContext.Default.DetectResponse);
    }

    private static DetectedProject Describe(PythonProject project)
    {
        return new(
            Name: project.Name,
            Directory: project.RelativeDirectory,
            Installer: InstallerName(project.Installer),
            Manifests: project.Manifests,
            RequiresPython: project.RequiresPython,
            TestExtras: project.TestExtras,
            TestGroups: project.TestGroups,
            DeclaresPytest: project.DeclaresPytest,
            DeclaresCoverage: project.DeclaresCoverage,
            Unsupported: project.Problem
        );
    }

    private static string InstallerName(PythonInstaller installer)
    {
        return installer switch
        {
            PythonInstaller.Pip => "pip",
            PythonInstaller.Uv => "uv",
            _ => "unsupported",
        };
    }
}
