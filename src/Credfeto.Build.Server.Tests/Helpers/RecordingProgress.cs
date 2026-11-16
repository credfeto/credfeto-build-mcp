using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Server.Tests.Helpers;

// Keeps every progress report a tool makes, as a client that sent a progress token would see them
internal sealed class RecordingProgress : IToolProgress
{
    private readonly Lock _lock = new();
    private readonly List<(double Progress, string Message)> _reports = [];

    public RecordingProgress(bool enabled)
    {
        this.Enabled = enabled;
    }

    public bool Enabled { get; }

    public IReadOnlyList<(double Progress, string Message)> Reports
    {
        get
        {
            lock (this._lock)
            {
                return [.. this._reports];
            }
        }
    }

    public ValueTask ReportAsync(double progress, string message, CancellationToken cancellationToken)
    {
        lock (this._lock)
        {
            this._reports.Add((progress, message));
        }

        return ValueTask.CompletedTask;
    }
}
