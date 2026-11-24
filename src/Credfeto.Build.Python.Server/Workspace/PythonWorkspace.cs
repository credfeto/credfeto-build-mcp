using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tools;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Python.Server.Workspace;

public sealed class PythonWorkspace : IPythonDetectionSource, IRepositoryChangeHandler, IDisposable
{
    private const string REQUIREMENTS_EXTENSION = ".txt";

    private readonly LanguageWorkspace<PythonDetection> _workspace;

    public PythonWorkspace(PythonDetection initial, IJobRunner runner, PythonLocator python)
    {
        ToolContext context = new(detection: this, runner: runner, python: python);
        this._workspace = new(
            initial: initial,
            language: new PythonWorkspaceLanguage(repositoryRoot: initial.RepositoryRoot, context: context)
        );
    }

    public McpServerPrimitiveCollection<McpServerTool> Tools => this._workspace.Tools;

    public PythonDetection Current => this._workspace.Current;

    public void Dispose()
    {
        this._workspace.Dispose();
    }

    public ValueTask<PythonDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        return this._workspace.RefreshAsync(cancellationToken);
    }

    public ValueTask RepositoryChangedAsync(CancellationToken cancellationToken)
    {
        return this._workspace.RepositoryChangedAsync(cancellationToken);
    }

    public bool IsTransientFailure(Exception exception)
    {
        return this._workspace.IsTransientFailure(exception);
    }

    // Manifests (requirements*.txt by their extension), lock files and the overrides file decide what detection
    // finds. Only the listed directories are watched, each on its own: a recursive watch would add one kernel watch
    // per directory of every virtual environment
    public static WatchFilter WatchFilterFor(PythonDetection detection)
    {
        return new(Extensions: [REQUIREMENTS_EXTENSION], IgnoredDirectories: TestSuiteScan.IgnoredDirectories)
        {
            FileNames = ManifestFiles.WatchedFileNames,
            Recursive = false,
            Directories = detection.WatchedDirectories,
        };
    }

    public static string Signature(PythonDetection detection)
    {
        StringBuilder builder = new();
        _ = builder.Append(detection.Reason).Append('\n').Append(detection.OverridesError).Append('\n');

        if (detection.TestSuite is { } testSuite)
        {
            _ = builder.Append("tests|").AppendJoin(separator: ',', values: testSuite.TestDirectories).Append('\n');
        }

        if (detection.Repository is not { } repository)
        {
            return builder.ToString();
        }

        _ = builder
            .Append(repository.Timeout.TotalMinutes.ToString(provider: CultureInfo.InvariantCulture))
            .Append('\n');

        foreach (PythonProject project in repository.Projects)
        {
            _ = builder
                .Append(project.Name)
                .Append('|')
                .Append(project.RelativeDirectory)
                .Append('|')
                .Append(project.Installer.ToString())
                .Append('|')
                .AppendJoin(separator: ',', values: project.Manifests.Order(StringComparer.Ordinal))
                .Append('\n');
        }

        return builder.ToString();
    }
}
