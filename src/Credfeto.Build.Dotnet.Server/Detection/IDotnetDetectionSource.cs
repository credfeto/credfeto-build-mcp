using Credfeto.Build.Server.Workspace;

namespace Credfeto.Build.Dotnet.Server.Detection;

// Detects .NET in the repository afresh, as it is now
public interface IDotnetDetectionSource : IDetectionSource<DotnetDetection>;
