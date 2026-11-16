using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Server.Tools;

// For a call made without a progress token, which has nowhere to send progress
public sealed class NoToolProgress : IToolProgress
{
    public static readonly NoToolProgress Instance = new();

    private NoToolProgress() { }

    public ValueTask ReportAsync(int progress, int total, string message, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
