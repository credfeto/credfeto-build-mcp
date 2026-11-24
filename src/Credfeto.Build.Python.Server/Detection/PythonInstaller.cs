namespace Credfeto.Build.Python.Server.Detection;

// How a project's dependencies are installed into its virtual environment: pip, or uv when the project has a uv.lock.
// A project managed by poetry, pipenv or pdm (or one whose manifest cannot be read) is Unsupported
public enum PythonInstaller
{
    Unsupported,
    Pip,
    Uv,
}
