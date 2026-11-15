using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tools;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Workspace;

internal sealed class DotnetWorkspaceLanguage : IWorkspaceLanguage<DotnetDetection>
{
    private readonly ToolContext _context;
    private readonly string _repositoryRoot;

    public DotnetWorkspaceLanguage(string repositoryRoot, ToolContext context)
    {
        this._repositoryRoot = repositoryRoot;
        this._context = context;
    }

    public ValueTask<DotnetDetection> DetectAsync(CancellationToken cancellationToken)
    {
        return DotnetDetector.DetectAsync(repositoryRoot: this._repositoryRoot, cancellationToken: cancellationToken);
    }

    public string Signature(DotnetDetection detection)
    {
        return DotnetWorkspace.Signature(detection);
    }

    public IReadOnlyList<McpServerTool> CreateTools(DotnetDetection detection)
    {
        return DotnetToolCatalog.Create(detection: detection, context: this._context);
    }

    // A solution or project file caught mid-write
    public bool IsTransientFailure(Exception exception)
    {
        return exception is XmlException;
    }
}
