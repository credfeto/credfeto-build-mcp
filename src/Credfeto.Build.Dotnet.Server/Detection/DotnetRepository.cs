using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Detection;

[DebuggerDisplay("SolutionPath={SolutionPath}, Projects={Projects.Count}, UnitTestProjects={UnitTestProjects.Count}")]
public sealed record DotnetRepository(
    string RepositoryRoot,
    string SolutionPath,
    string SolutionDirectory,
    IReadOnlyList<DotnetProject> Projects,
    IReadOnlyList<DotnetProject> UnitTestProjects,
    TimeSpan Timeout
);
