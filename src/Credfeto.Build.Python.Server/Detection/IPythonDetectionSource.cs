using Credfeto.Build.Server.Workspace;

namespace Credfeto.Build.Python.Server.Detection;

// Detects the Python projects in the repository afresh, as they are now
public interface IPythonDetectionSource : IDetectionSource<PythonDetection>;
