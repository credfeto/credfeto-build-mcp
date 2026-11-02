using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

/// <summary>A repository that never changes, so a tool test needs nothing on disk.</summary>
internal sealed class FixedDetectionSource : IDotnetDetectionSource
{
    private readonly DotnetDetection _detection;

    public FixedDetectionSource(DotnetDetection detection)
    {
        this._detection = detection;
    }

    public ValueTask<DotnetDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(this._detection);
    }
}
