using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Dotnet.Server.Detection;

public interface IDotnetDetectionSource
{
    /// <summary>Detects .NET in the repository afresh, as it is now.</summary>
    ValueTask<DotnetDetection> RefreshAsync(CancellationToken cancellationToken);
}
