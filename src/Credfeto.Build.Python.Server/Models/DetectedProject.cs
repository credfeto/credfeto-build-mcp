using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Models;

[DebuggerDisplay("Name={Name}, Directory={Directory}, Installer={Installer}")]
public sealed record DetectedProject(
    string Name,
    string Directory,
    string Installer,
    IReadOnlyList<string> Manifests,
    string? RequiresPython,
    IReadOnlyList<string> TestExtras,
    IReadOnlyList<string> TestGroups,
    bool DeclaresPytest,
    bool DeclaresCoverage,
    string? Unsupported
);
