using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Tests.Helpers;

// A repository that never changes, so a tool test needs nothing on disk
internal sealed class FixedDetectionSource : IPythonDetectionSource
{
    private readonly PythonDetection _detection;

    public FixedDetectionSource(PythonDetection detection)
    {
        this._detection = detection;
    }

    public ValueTask<PythonDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(this._detection);
    }
}
