using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Server.Tools;

// Sends notifications/progress for the request a tool is answering, when the client asked for them
public interface IToolProgress
{
    // False when the request carried no progress token, so nothing would be sent
    bool Enabled { get; }

    ValueTask ReportAsync(double progress, string message, CancellationToken cancellationToken);
}
