using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Models;

[DebuggerDisplay("Projects={Projects.Count}")]
public sealed record DetectResponse(
    string RepositoryRoot,
    string? Interpreter,
    IReadOnlyList<DetectedProject> Projects,
    DetectedTestSuite? TestSuiteWithoutManifest,
    int? TimeoutMinutes,
    string Reason,
    string? OverridesError
);
