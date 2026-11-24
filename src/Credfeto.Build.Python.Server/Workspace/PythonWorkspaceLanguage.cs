using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tools;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Python.Server.Workspace;

internal sealed class PythonWorkspaceLanguage : IWorkspaceLanguage<PythonDetection>
{
    private readonly ToolContext _context;
    private readonly string _repositoryRoot;

    public PythonWorkspaceLanguage(string repositoryRoot, ToolContext context)
    {
        this._repositoryRoot = repositoryRoot;
        this._context = context;
    }

    public ValueTask<PythonDetection> DetectAsync(CancellationToken cancellationToken)
    {
        return PythonDetector.DetectAsync(repositoryRoot: this._repositoryRoot, cancellationToken: cancellationToken);
    }

    public string Signature(PythonDetection detection)
    {
        return PythonWorkspace.Signature(detection);
    }

    public IReadOnlyList<McpServerTool> CreateTools(PythonDetection detection)
    {
        return PythonToolCatalog.Create(detection: detection, context: this._context);
    }

    // Invalid TOML is reported as the reason, not thrown, so nothing here is transient
    public bool IsTransientFailure(Exception exception)
    {
        return false;
    }
}
