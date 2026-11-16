using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class ToolContext
{
    public ToolContext(INodeDetectionSource detection)
    {
        this.Detection = detection;
    }

    public INodeDetectionSource Detection { get; }

    public static NodeRepository RepositoryOf(NodeDetection detection)
    {
        return detection.Repository
            ?? throw new ToolUnavailableException(
                "There is no supported npm repository any more (" + detection.Reason + ")"
            );
    }
}
