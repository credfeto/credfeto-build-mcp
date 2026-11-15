using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tools;
using Credfeto.Build.Server.Commands;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Workspace;

public sealed class DotnetWorkspace : IDotnetDetectionSource, IRepositoryChangeHandler, IDisposable
{
    // Solutions, projects and the overrides file decide what detection finds; build output and git internals never do
    public static readonly WatchFilter WatchedFiles = new(
        Extensions: [".csproj", ".sln", ".slnx"],
        IgnoredDirectories: ["bin", "obj", ".git"]
    );

    private readonly LanguageWorkspace<DotnetDetection> _workspace;

    public DotnetWorkspace(
        DotnetDetection initial,
        IJobRunner runner,
        ICommandRunner commands,
        BuildtestLocator buildtest
    )
    {
        ToolContext context = new(runner: runner, commands: commands, detection: this, buildtest: buildtest);
        this._workspace = new(
            initial: initial,
            language: new DotnetWorkspaceLanguage(repositoryRoot: initial.RepositoryRoot, context: context)
        );
    }

    public McpServerPrimitiveCollection<McpServerTool> Tools => this._workspace.Tools;

    public DotnetDetection Current => this._workspace.Current;

    public void Dispose()
    {
        this._workspace.Dispose();
    }

    public ValueTask<DotnetDetection> RefreshAsync(CancellationToken cancellationToken)
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

    public static string Signature(DotnetDetection detection)
    {
        StringBuilder builder = new();
        _ = builder.Append(detection.Reason).Append('\n').Append(detection.OverridesError).Append('\n');

        if (detection.Repository is not { } repository)
        {
            return builder.ToString();
        }

        _ = builder
            .Append(repository.SolutionPath)
            .Append('\n')
            .Append(repository.Timeout.TotalMinutes.ToString(provider: CultureInfo.InvariantCulture))
            .Append('\n');

        foreach (DotnetProject project in repository.Projects)
        {
            _ = builder
                .Append(project.Name)
                .Append('|')
                .Append(project.RelativePath)
                .Append('|')
                .Append(((int)project.Kind).ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        foreach (DotnetProject project in repository.UnitTestProjects)
        {
            _ = builder.Append("unit|").Append(project.Name).Append('\n');
        }

        return builder.ToString();
    }
}
