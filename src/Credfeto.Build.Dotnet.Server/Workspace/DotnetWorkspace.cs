using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Workspace;

/// <summary>
///     The repository as the server last saw it, and the tool list that goes with it. A refresh re-runs detection
///     and, when the result differs, swaps the tools in one change, so the server sends a single
///     <c>notifications/tools/list_changed</c>.
/// </summary>
public sealed class DotnetWorkspace : IDotnetDetectionSource, IDisposable
{
    private readonly ToolContext _context;
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);
    private readonly string _repositoryRoot;
    private DotnetDetection _current;
    private string _signature;

    public DotnetWorkspace(DotnetDetection initial, IJobRunner runner, BuildtestLocator buildtest)
    {
        this._repositoryRoot = initial.RepositoryRoot;
        this._current = initial;
        this._signature = Signature(initial);
        this._context = new(runner: runner, detection: this, buildtest: buildtest);
        this.Tools = [];

        foreach (McpServerTool tool in DotnetToolCatalog.Create(detection: initial, context: this._context))
        {
            this.Tools.Add(tool);
        }
    }

    /// <summary>The live tool collection the MCP server publishes; it changes when the repository does.</summary>
    public McpServerPrimitiveCollection<McpServerTool> Tools { get; }

    public DotnetDetection Current => Volatile.Read(ref this._current);

    public void Dispose()
    {
        this._gate.Dispose();
    }

    public async ValueTask<DotnetDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        await this._gate.WaitAsync(cancellationToken);

        try
        {
            DotnetDetection fresh = await DotnetDetector.DetectAsync(repositoryRoot: this._repositoryRoot, cancellationToken: cancellationToken);
            string signature = Signature(fresh);

            // Only one refresh runs at a time, so the signature needs no other lock
            if (StringComparer.Ordinal.Equals(x: signature, y: this._signature))
            {
                return fresh;
            }

            this._signature = signature;
            Volatile.Write(location: ref this._current, value: fresh);
            this.Replace(fresh);

            return fresh;
        }
        finally
        {
            this._gate.Release();
        }
    }

    private void Replace(DotnetDetection detection)
    {
        IReadOnlyList<McpServerTool> tools = DotnetToolCatalog.Create(detection: detection, context: this._context);

        using (this.Tools.DeferChangedEvents())
        {
            this.Tools.Clear();

            foreach (McpServerTool tool in tools)
            {
                this.Tools.Add(tool);
            }
        }
    }

    /// <summary>Everything about a detection that shapes the tool list or a tool's behaviour.</summary>
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
            _ = builder.Append(project.Name).Append('|').Append(project.RelativePath).Append('|').Append(((int)project.Kind).ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        foreach (DotnetProject project in repository.UnitTestProjects)
        {
            _ = builder.Append("unit|").Append(project.Name).Append('\n');
        }

        return builder.ToString();
    }
}
