using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tools;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Node.Server.Workspace;

public sealed class NodeWorkspace : INodeDetectionSource, IRepositoryChangeHandler, IDisposable
{
    private readonly LanguageWorkspace<NodeDetection> _workspace;

    public NodeWorkspace(NodeDetection initial, IJobRunner runner, NpmLocator npm)
    {
        ToolContext context = new(detection: this, runner: runner, npm: npm);
        this._workspace = new(
            initial: initial,
            language: new NodeWorkspaceLanguage(repositoryRoot: initial.RepositoryRoot, context: context)
        );
    }

    public McpServerPrimitiveCollection<McpServerTool> Tools => this._workspace.Tools;

    public NodeDetection Current => this._workspace.Current;

    public void Dispose()
    {
        this._workspace.Dispose();
    }

    public ValueTask<NodeDetection> RefreshAsync(CancellationToken cancellationToken)
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

    // Package manifests, lock files and the overrides file decide what detection finds. Only the listed directories
    // are watched, each on its own: a recursive watch would add one kernel watch per directory of node_modules
    public static WatchFilter WatchFilterFor(NodeDetection detection)
    {
        return new(Extensions: [], IgnoredDirectories: [WorkspacePattern.IGNORED_DIRECTORY, ".git"])
        {
            FileNames = [PackageManifestReader.FILE_NAME, .. LockFiles.FileNames],
            Recursive = false,
            Directories = detection.WatchedDirectories,
        };
    }

    public static string Signature(NodeDetection detection)
    {
        StringBuilder builder = new();
        _ = builder.Append(detection.Reason).Append('\n').Append(detection.OverridesError).Append('\n');

        if (detection.Repository is not { } repository)
        {
            return builder.ToString();
        }

        _ = builder
            .Append(repository.Timeout.TotalMinutes.ToString(provider: CultureInfo.InvariantCulture))
            .Append('|')
            .Append(repository.TestScript)
            .Append('|')
            .Append(repository.CoverageScript)
            .Append('|')
            .Append(repository.CoverageFile)
            .Append('\n');

        foreach (NodeInstallRoot installRoot in repository.InstallRoots)
        {
            _ = builder
                .Append("root|")
                .Append(installRoot.RelativeDirectory)
                .Append('|')
                .Append(installRoot.LockFile)
                .Append('\n');
        }

        foreach (NodePackage package in repository.Packages)
        {
            // With node.testScript set, whether a package has that script decides whether test runs it
            bool hasTestScript = repository.TestScript is { } testScript && package.HasScript(testScript);
            bool hasCoverageScript =
                CoverageScripts.For(package: package, overrideScript: repository.CoverageScript) is not null;
            _ = builder
                .Append(package.Name)
                .Append('|')
                .Append(package.RelativeDirectory)
                .Append('|')
                .Append(package.InstallRoot)
                .Append('|')
                .AppendJoin(separator: ',', values: package.Scripts.Order(StringComparer.Ordinal))
                .Append('|')
                .Append(hasTestScript ? '1' : '0')
                .Append(hasCoverageScript ? '1' : '0')
                .Append('\n');
        }

        return builder.ToString();
    }
}
