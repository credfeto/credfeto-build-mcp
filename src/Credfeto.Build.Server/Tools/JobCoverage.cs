using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Build.Core.Coverage;

namespace Credfeto.Build.Server.Tools;

// What a job measures: the Cobertura files it reads, where its report goes (null for none), which COVERAGE.md
// section that report replaces, and optionally the component each file's figures are reported as. ReportComponent,
// when set, limits the report to that component's row (a run of one project or package)
[DebuggerDisplay("Files={Files.Count}, ReportPath={ReportPath}, Language={Language.Heading}")]
public sealed record JobCoverage(
    IReadOnlyList<string> Files,
    string? ReportPath,
    CoverageLanguage Language,
    IReadOnlyDictionary<string, string>? Components
)
{
    public string? ReportComponent { get; init; }
}
