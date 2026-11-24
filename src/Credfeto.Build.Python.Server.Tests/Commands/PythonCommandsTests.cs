using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Python.Server.Tests.Commands;

public sealed class PythonCommandsTests : TestBase
{
    private const string ROOT = "/work/repo";
    private const string PYTHON = "/usr/bin/python3";
    private const string VENV_PYTHON = "/work/repo/.venv/bin/python";

    private static readonly string[] PipInstall =
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

    [Fact]
    public void APipProjectGetsAVenvThenAWheelOnlyInstallThenTheStamp()
    {
        IReadOnlyList<ProcessStep> steps = PythonCommands.Install(Context(Project(declares: true)));

        AssertNames(expected: ["venv example", "install example (wheels only)", "stamp example"], steps: steps);
        Assert.Equal(expected: PYTHON, actual: steps[0].FileName);
        Assert.Equal(
            expected: ["-m", "venv", "--clear", "/work/repo/.venv"],
            actual: steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: VENV_PYTHON, actual: steps[1].FileName);
        Assert.Equal(
            expected: [.. PipInstall, "-r", "requirements.txt", "httpx>=0.27", "pytest", "coverage"],
            actual: steps[1].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ROOT, actual: steps[1].WorkingDirectory);
        Assert.True(
            condition: steps.All(static step => step.StopJobOnFailure),
            userMessage: "a failed install stops the job"
        );
        Assert.Equal(expected: "/work/repo/.venv", actual: steps[1].Environment["VIRTUAL_ENV"]);
        Assert.StartsWith(
            expectedStartString: "/work/repo/.venv/bin",
            actualString: steps[1].Environment["PATH"],
            comparisonType: StringComparison.Ordinal
        );
        Assert.Equal(expected: "/work/repo/.venv/" + VirtualEnvironment.STAMP_FILE, actual: steps[2].Arguments[2]);
        Assert.Equal(expected: "abc123", actual: steps[2].Arguments[3]);
    }

    [Fact]
    public void AnExistingVenvIsReusedAndUndeclaredTestToolsAreAddedUnpinned()
    {
        InstallContext context = Context(Project(declares: false)) with { VenvExists = true };

        IReadOnlyList<ProcessStep> steps = PythonCommands.Install(context);

        AssertNames(expected: ["install example (wheels only)", "stamp example"], steps: steps);
        Assert.Equal(
            expected: [.. PipInstall, "-r", "requirements.txt", "httpx>=0.27", "pytest", "coverage"],
            actual: steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void AUvProjectSyncsFrozenWithoutBuildingAndAddsMissingTools()
    {
        PythonProject project = new(Name: "example", RelativeDirectory: "src", Installer: PythonInstaller.Uv)
        {
            TestExtras = ["dev"],
            TestGroups = ["test"],
            DeclaresPytest = true,
        };
        InstallContext context = Context(project) with { Uv = "/usr/bin/uv" };

        IReadOnlyList<ProcessStep> steps = PythonCommands.Install(context);

        AssertNames(
            expected: ["sync example (no builds)", "install test tools example (no builds)", "stamp example"],
            steps: steps
        );
        Assert.Equal(
            expected:
            [
                "sync",
                "--frozen",
                "--no-build",
                "--no-install-workspace",
                "--python",
                PYTHON,
                "--extra",
                "dev",
                "--group",
                "test",
            ],
            actual: steps[0].Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "/work/repo/src", actual: steps[0].WorkingDirectory);
        Assert.Equal(expected: "never", actual: steps[0].Environment["UV_PYTHON_DOWNLOADS"]);
        Assert.Equal(expected: "/work/repo/src/.venv", actual: steps[0].Environment["UV_PROJECT_ENVIRONMENT"]);
        Assert.Equal(
            expected: ["pip", "install", "--python", "/work/repo/src/.venv/bin/python", "--no-build", "coverage"],
            actual: steps[1].Arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void PytestRunsWithTheVenvInterpreterFromTheProject()
    {
        ProcessStep step = PythonCommands.Pytest(Context(Project(declares: true)));

        Assert.Equal(expected: "test example", actual: step.Name);
        Assert.Equal(expected: VENV_PYTHON, actual: step.FileName);
        Assert.Equal(
            expected: ["-m", "pytest", "-p", "no:cacheprovider", "--color=no", "-rfE"],
            actual: step.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ROOT, actual: step.WorkingDirectory);
    }

    [Fact]
    public void PylintSeesTheVenvPackagesAndTakesTheSuperLinterConfiguration()
    {
        InstallContext context = Context(Project(declares: true));

        ProcessStep hook = PythonCommands.Pylint(
            context: context,
            pylint: "/usr/bin/pylint",
            rcFile: null,
            files: ["a.py"]
        );
        ProcessStep superLinter = PythonCommands.Pylint(
            context: context,
            pylint: "/usr/bin/pylint",
            rcFile: ".github/linters/.python-lint",
            files: ["a.py"]
        );
        ProcessStep flake8 = PythonCommands.Flake8(context: context, flake8: "/usr/bin/flake8", files: ["a.py"]);

        Assert.Equal(expected: "/work/repo/.venv/lib/python3.13/site-packages", actual: hook.Environment["PYTHONPATH"]);
        Assert.Equal(expected: ["a.py"], actual: hook.Arguments, comparer: StringComparer.Ordinal);
        Assert.Equal(
            expected: ["--rcfile", ".github/linters/.python-lint", "a.py"],
            actual: superLinter.Arguments,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: ROOT, actual: flake8.WorkingDirectory);
        Assert.Equal(expected: ["a.py"], actual: flake8.Arguments, comparer: StringComparer.Ordinal);
    }

    private static void AssertNames(string[] expected, IReadOnlyList<ProcessStep> steps)
    {
        Assert.Equal(
            expected: expected,
            actual: steps.Select(static step => step.Name),
            comparer: StringComparer.Ordinal
        );
    }

    private static PythonProject Project(bool declares)
    {
        return new(Name: "example", RelativeDirectory: ".", Installer: PythonInstaller.Pip)
        {
            RequirementFiles = ["requirements.txt"],
            Dependencies = ["httpx>=0.27"],
            TestRequirements = declares ? ["pytest", "coverage"] : [],
            DeclaresPytest = declares,
            DeclaresCoverage = declares,
        };
    }

    private static InstallContext Context(PythonProject project)
    {
        return new(
            RepositoryRoot: ROOT,
            Project: project,
            Interpreter: PYTHON,
            Version: new(Major: 3, Minor: 13, Patch: 1),
            Environment: VirtualEnvironment.For(repositoryRoot: ROOT, project: project),
            Stamp: "abc123"
        );
    }
}
