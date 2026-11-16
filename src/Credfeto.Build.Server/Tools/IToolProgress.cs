using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Server.Tools;

// Where a long-running tool call reports how far it has got; a client that asked for no progress gets none
public interface IToolProgress
{
    ValueTask ReportAsync(int progress, int total, string message, CancellationToken cancellationToken);
}
