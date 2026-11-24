using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Tools;

// What the global pre-commit hook and super-linter run for Python, so a clean lint means a clean commit and a clean
// CI lint: flake8 and pylint as the hook runs them (from the repository root, each finding its own configuration
// there), and pylint again with --rcfile .github/linters/.python-lint when the repository has it, as super-linter
// (v8, VALIDATE_PYTHON_PYLINT) runs it. super-linter v8 has no plain VALIDATE_PYTHON, so it runs no flake8
public sealed class LintTool : BuildTool
{
    public const string SUPER_LINTER_PYLINT_RC = ".github/linters/.python-lint";

    private static readonly SchemaProperty Scope = new(
        Name: "scope",
        Description: "all lints every tracked *.py file; changed lints only those changed against HEAD and new untracked ones",
        Values: [LintFiles.SCOPE_ALL, LintFiles.SCOPE_CHANGED],
        Pattern: null,
        DefaultValue: LintFiles.SCOPE_ALL,
        Required: false
    );

    private readonly ToolContext _context;
    private readonly SchemaProperty _package;

    public LintTool(PythonRepository repository, ToolContext context)
        : this(
            context: context,
            package: ProjectChoice.Property(
                repository: repository,
                description: "all, or the project whose files are linted"
            )
        ) { }

    private LintTool(ToolContext context, SchemaProperty package)
        : base(
            name: ToolNames.LINT,
            title: "Lint Python",
            description: "Starts a job that runs what the global pre-commit hook and super-linter run for Python: flake8, then pylint (with the project's .venv site-packages on PYTHONPATH so its imports resolve), from the repository root with the repository's own configuration, then pylint --rcfile .github/linters/.python-lint when that file exists, as super-linter does. It stops at the first linter that fails. It installs first, as restore does, when the venv is missing or its manifests changed. Returns the job id at once; call job_result to wait for its outcome",
            schema: [Scope, package],
            readOnly: false
        )
    {
        this._context = context;
        this._package = package;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        string scope = arguments.GetChoice(Scope) ?? LintFiles.SCOPE_ALL;
        PythonRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        IReadOnlyList<PythonProject> projects = ProjectChoice.Select(
            arguments: arguments,
            property: this._package,
            repository: repository
        );
        IReadOnlyList<string> files = await LintFiles.FindAsync(
            repositoryRoot: repository.RepositoryRoot,
            scope: scope,
            cancellationToken: cancellationToken
        );
        IReadOnlyList<InstallContext> prepared = await ProjectSetup.PrepareAsync(
            context: this._context,
            repository: repository,
            projects: projects,
            cancellationToken: cancellationToken
        );
        IReadOnlyList<ProcessStep> lints = this.LintSteps(repository: repository, prepared: prepared, files: files);

        return await JobStarter.StartAsync(
            runner: this._context.Runner,
            repositoryRoot: repository.RepositoryRoot,
            timeout: repository.Timeout,
            kind: ToolNames.LINT,
            steps: [.. prepared.SelectMany(ProjectSetup.InstallIfNeeded), .. lints],
            testParser: null,
            coverageFiles: [],
            changedFiles: null,
            cancellationToken: cancellationToken
        );
    }

    private IReadOnlyList<ProcessStep> LintSteps(
        PythonRepository repository,
        IReadOnlyList<InstallContext> prepared,
        IReadOnlyList<string> files
    )
    {
        string flake8 = this._context.Python.Require(PythonLocator.FLAKE8);
        string pylint = this._context.Python.Require(PythonLocator.PYLINT);
        bool superLinterRc = File.Exists(Path.Combine(path1: repository.RepositoryRoot, path2: SUPER_LINTER_PYLINT_RC));
        List<ProcessStep> steps = [];

        foreach (InstallContext context in prepared)
        {
            IReadOnlyList<string> own = LintFiles.For(
                project: context.Project,
                allProjects: repository.SupportedProjects,
                files: files
            );

            if (own.Count == 0)
            {
                continue;
            }

            steps.Add(PythonCommands.Flake8(context: context, flake8: flake8, files: own));
            steps.Add(PythonCommands.Pylint(context: context, pylint: pylint, rcFile: null, files: own));

            if (superLinterRc)
            {
                steps.Add(
                    PythonCommands.Pylint(context: context, pylint: pylint, rcFile: SUPER_LINTER_PYLINT_RC, files: own)
                );
            }
        }

        return steps.Count != 0 ? steps : throw new ToolUnavailableException("There are no Python files to lint");
    }
}
