using Credfeto.Build.Server.Workspace;

namespace Credfeto.Build.Node.Server.Detection;

// Detects the npm packages in the repository afresh, as they are now
public interface INodeDetectionSource : IDetectionSource<NodeDetection>;
