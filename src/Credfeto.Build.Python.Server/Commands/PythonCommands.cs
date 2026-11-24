using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Commands;

// The command vectors the tools run. Every program is started directly with an argument vector, never through a
// shell. Installs never run package code: pip takes wheels only (--only-binary :all:) and uv never builds
// (--no-build), and the project itself is never built or installed, so a package with no wheel fails the install
public static class PythonCommands
{
    public const string PYTEST_MODULE = "pytest";
    public const string COVERAGE_MODULE = "coverage";

    private const string STAMP_SCRIPT =
        "import pathlib, sys; pathlib.Path(sys.argv[1]).write_text(sys.argv[2] + '\\n')";

    private static readonly IReadOnlyList<string> PipInstall =
    [
        "-m",
        "pip",
        "install",
        "--require-virtualenv",
        "--no-input",
        "--disable-pip-version-check",
        "--only-binary",
        ":all:",
    ];

    public static IReadOnlyDictionary<string, string> Environment(VirtualEnvironment environment)
    {
        string path = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VIRTUAL_ENV"] = environment.Directory,
            ["PATH"] = path.Length == 0 ? environment.BinDirectory : environment.BinDirectory + ":" + path,
            ["PYTHONDONTWRITEBYTECODE"] = "1",
            ["PYTHONUNBUFFERED"] = "1",
            ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1",
            ["PIP_NO_INPUT"] = "1",
            ["NO_COLOR"] = "1",
            ["UV_PYTHON_DOWNLOADS"] = "never",
            ["UV_NO_PROGRESS"] = "1",
            ["UV_PROJECT_ENVIRONMENT"] = environment.Directory,
        };
    }

    // The test tools the project does not declare, installed unpinned so a test or coverage run can work at all
    public static IReadOnlyList<string> MissingTools(PythonProject project)
    {
        return
        [
            .. project.DeclaresPytest ? [] : (string[])[PYTEST_MODULE],
            .. project.DeclaresCoverage ? [] : (string[])[COVERAGE_MODULE],
        ];
    }

    public static IReadOnlyList<ProcessStep> Install(InstallContext context)
    {
        return context.Project.Installer == PythonInstaller.Uv ? UvInstall(context) : PipInstallSteps(context);
    }

    public static ProcessStep Pytest(InstallContext context)
    {
        return Step(
            name: "test " + context.Project.Name,
            fileName: context.Environment.Python,
            arguments: ["-m", PYTEST_MODULE, "-p", "no:cacheprovider", "--color=no", "-rfE"],
            context: context
        );
    }

    public static ProcessStep Flake8(InstallContext context, string flake8, IReadOnlyList<string> files)
    {
        return Step(name: "flake8 " + context.Project.Name, fileName: flake8, arguments: files, context: context) with
        {
            WorkingDirectory = context.RepositoryRoot,
        };
    }

    // pylint is not installed in the venv; the venv's site-packages on PYTHONPATH lets it resolve the project's own
    // imports, as the pre-commit hook's run-pylint wrapper does with its venv
    public static ProcessStep Pylint(InstallContext context, string pylint, string? rcFile, IReadOnlyList<string> files)
    {
        Dictionary<string, string> environment = new(Environment(context.Environment), StringComparer.Ordinal)
        {
            ["PYTHONPATH"] = context.Environment.SitePackages(context.Version),
        };

        return new(
            Name: "pylint " + context.Project.Name + (rcFile is null ? string.Empty : " (" + rcFile + ")"),
            FileName: pylint,
            Arguments: rcFile is null ? files : ["--rcfile", rcFile, .. files],
            WorkingDirectory: context.RepositoryRoot,
            Environment: environment
        );
    }

    private static IReadOnlyList<ProcessStep> PipInstallSteps(InstallContext context)
    {
        PythonProject project = context.Project;
        List<ProcessStep> steps = [];

        if (!context.VenvExists)
        {
            steps.Add(
                Stopping(
                    Step(
                        name: "venv " + project.Name,
                        fileName: context.Interpreter,
                        arguments: ["-m", "venv", "--clear", context.Environment.Directory],
                        context: context
                    )
                )
            );
        }

        string[] requirements =
        [
            .. project.RequirementFiles.SelectMany(static file => (string[])["-r", file]),
            .. project.Dependencies,
            .. project.TestRequirements,
            .. MissingTools(project),
        ];

        if (requirements.Length != 0)
        {
            steps.Add(
                Stopping(
                    Step(
                        name: "install " + project.Name + " (wheels only)",
                        fileName: context.Environment.Python,
                        arguments: [.. PipInstall, .. requirements],
                        context: context
                    )
                )
            );
        }

        steps.Add(StampStep(context));

        return steps;
    }

    private static IReadOnlyList<ProcessStep> UvInstall(InstallContext context)
    {
        PythonProject project = context.Project;
        string uv = context.Uv ?? throw new InvalidOperationException("uv is required for a uv project");
        List<ProcessStep> steps =
        [
            Stopping(
                Step(
                    name: "sync " + project.Name + " (no builds)",
                    fileName: uv,
                    arguments:
                    [
                        "sync",
                        "--frozen",
                        "--no-build",
                        "--no-install-workspace",
                        "--python",
                        context.Interpreter,
                        .. project.TestExtras.SelectMany(static extra => (string[])["--extra", extra]),
                        .. project.TestGroups.SelectMany(static group => (string[])["--group", group]),
                    ],
                    context: context
                )
            ),
        ];
        IReadOnlyList<string> missing = MissingTools(project);

        if (missing.Count != 0)
        {
            steps.Add(
                Stopping(
                    Step(
                        name: "install test tools " + project.Name + " (no builds)",
                        fileName: uv,
                        arguments: ["pip", "install", "--python", context.Environment.Python, "--no-build", .. missing],
                        context: context
                    )
                )
            );
        }

        steps.Add(StampStep(context));

        return steps;
    }

    private static ProcessStep StampStep(InstallContext context)
    {
        return Stopping(
            Step(
                name: "stamp " + context.Project.Name,
                fileName: context.Interpreter,
                arguments: ["-c", STAMP_SCRIPT, context.Environment.StampPath, context.Stamp],
                context: context
            )
        );
    }

    private static ProcessStep Stopping(ProcessStep step)
    {
        return step with { StopJobOnFailure = true };
    }

    private static ProcessStep Step(
        string name,
        string fileName,
        IReadOnlyList<string> arguments,
        InstallContext context
    )
    {
        return new(
            Name: name,
            FileName: fileName,
            Arguments: arguments,
            WorkingDirectory: context.Environment.ProjectDirectory,
            Environment: Environment(context.Environment)
        );
    }
}
