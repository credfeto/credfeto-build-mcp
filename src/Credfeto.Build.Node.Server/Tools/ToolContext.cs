using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class ToolContext
{
    public ToolContext(INodeDetectionSource detection, IJobRunner runner, NpmLocator npm, MachineResources resources)
    {
        this.Detection = detection;
        this.Runner = runner;
        this.Npm = npm;
        this.Resources = resources;
    }

    public INodeDetectionSource Detection { get; }

    public IJobRunner Runner { get; }

    public NpmLocator Npm { get; }

    public MachineResources Resources { get; }

    public static NodeRepository RepositoryOf(NodeDetection detection)
    {
        return detection.Repository
            ?? throw new ToolUnavailableException(
                "There is no supported npm repository any more (" + detection.Reason + ")"
            );
    }

    // Every tool call detects the repository afresh, so a job never runs against packages that have gone
    public async ValueTask<NodeRepository> CurrentRepositoryAsync(CancellationToken cancellationToken)
    {
        return RepositoryOf(await this.Detection.RefreshAsync(cancellationToken));
    }
}
