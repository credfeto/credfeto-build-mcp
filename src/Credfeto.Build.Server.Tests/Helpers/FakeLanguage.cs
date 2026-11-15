using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Workspace;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Tests.Helpers;

// Detects whatever the test last set, and offers one fake tool per name
internal sealed class FakeLanguage : IWorkspaceLanguage<FakeDetection>
{
    public FakeLanguage(FakeDetection next)
    {
        this.Next = next;
    }

    public FakeDetection Next { get; set; }

    public int Detections { get; private set; }

    public ValueTask<FakeDetection> DetectAsync(CancellationToken cancellationToken)
    {
        this.Detections++;

        return ValueTask.FromResult(this.Next);
    }

    public string Signature(FakeDetection detection)
    {
        return detection.Signature;
    }

    public IReadOnlyList<McpServerTool> CreateTools(FakeDetection detection)
    {
        return [.. detection.ToolNames.Select(static name => new FakeTool(name))];
    }

    public bool IsTransientFailure(Exception exception)
    {
        return exception is InvalidDataException;
    }
}
