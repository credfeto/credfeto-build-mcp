using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Tests.Helpers;

// Detects the repository on disk afresh on every call, as the workspace does
internal sealed class LiveDetectionSource : IPythonDetectionSource
{
    private readonly string _repositoryRoot;

    public LiveDetectionSource(string repositoryRoot)
    {
        this._repositoryRoot = repositoryRoot;
    }

    public ValueTask<PythonDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        return PythonDetector.DetectAsync(repositoryRoot: this._repositoryRoot, cancellationToken: cancellationToken);
    }
}
