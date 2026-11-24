using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Credfeto.Build.Python.Server.Detection;

[DebuggerDisplay("Projects={Projects.Count}, Timeout={Timeout}")]
public sealed record PythonRepository(string RepositoryRoot, IReadOnlyList<PythonProject> Projects, TimeSpan Timeout)
{
    // The projects the job tools can run on: those whose installer is supported
    public IReadOnlyList<PythonProject> SupportedProjects =>
        [.. this.Projects.Where(static project => project.IsSupported)];
}
