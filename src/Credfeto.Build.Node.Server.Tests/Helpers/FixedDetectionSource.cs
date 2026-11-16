using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Tests.Helpers;

// A repository that never changes, so a tool test needs nothing on disk
internal sealed class FixedDetectionSource : INodeDetectionSource
{
    private readonly NodeDetection _detection;

    public FixedDetectionSource(NodeDetection detection)
    {
        this._detection = detection;
    }

    public ValueTask<NodeDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(this._detection);
    }
}
