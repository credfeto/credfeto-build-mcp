using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Workspace;

// The language-specific half of a workspace: how to detect it, when two detections differ, and its tools
public interface IWorkspaceLanguage<TDetection>
{
    ValueTask<TDetection> DetectAsync(CancellationToken cancellationToken);

    // Two detections with the same signature offer the same tools with the same schemas
    string Signature(TDetection detection);

    IReadOnlyList<McpServerTool> CreateTools(TDetection detection);

    bool IsTransientFailure(Exception exception);
}
