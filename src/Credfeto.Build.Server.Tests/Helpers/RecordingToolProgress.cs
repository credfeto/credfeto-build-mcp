using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Server.Tests.Helpers;

// Keeps each progress report a tool sends, so a test can check what the client would have seen
internal sealed class RecordingToolProgress : IToolProgress
{
    private readonly Lock _lock = new();
    private readonly List<string> _reports = [];

    public IReadOnlyList<string> Reports
    {
        get
        {
            lock (this._lock)
            {
                return [.. this._reports];
            }
        }
    }

    public ValueTask ReportAsync(int progress, int total, string message, CancellationToken cancellationToken)
    {
        string report =
            progress.ToString(CultureInfo.InvariantCulture)
            + "/"
            + total.ToString(CultureInfo.InvariantCulture)
            + " "
            + message;

        lock (this._lock)
        {
            this._reports.Add(report);
        }

        return ValueTask.CompletedTask;
    }
}
