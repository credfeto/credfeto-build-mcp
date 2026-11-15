using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Server.Workspace;

public interface IDetectionSource<TDetection>
{
    // Detects the language in the repository afresh, as it is now
    ValueTask<TDetection> RefreshAsync(CancellationToken cancellationToken);
}
