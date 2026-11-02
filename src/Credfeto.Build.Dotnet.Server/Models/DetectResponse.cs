using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Models;

[DebuggerDisplay("Solution={Solution}, Projects={Projects.Count}, UnitTestProjects={UnitTestProjects.Count}")]
public sealed record DetectResponse(
    string RepositoryRoot,
    string Solution,
    string SolutionDirectory,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> UnitTestProjects,
    IReadOnlyList<string> IntegrationTestProjects,
    IReadOnlyList<string> BenchmarkTestProjects,
    int TimeoutMinutes,
    string? OverridesError
);
