using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Tools;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Node.Server.Workspace;

internal sealed class NodeWorkspaceLanguage : IWorkspaceLanguage<NodeDetection>
{
    private readonly ToolContext _context;
    private readonly string _repositoryRoot;

    public NodeWorkspaceLanguage(string repositoryRoot, ToolContext context)
    {
        this._repositoryRoot = repositoryRoot;
        this._context = context;
    }

    public ValueTask<NodeDetection> DetectAsync(CancellationToken cancellationToken)
    {
        return NodeDetector.DetectAsync(repositoryRoot: this._repositoryRoot, cancellationToken: cancellationToken);
    }

    public string Signature(NodeDetection detection)
    {
        return NodeWorkspace.Signature(detection);
    }

    public IReadOnlyList<McpServerTool> CreateTools(NodeDetection detection)
    {
        return NodeToolCatalog.Create(detection: detection, context: this._context);
    }

    // Invalid JSON is reported as the reason, not thrown, so nothing here is transient
    public bool IsTransientFailure(Exception exception)
    {
        return false;
    }
}
