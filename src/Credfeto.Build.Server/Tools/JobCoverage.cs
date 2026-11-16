using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Build.Core.Coverage;

namespace Credfeto.Build.Server.Tools;

// What a job measures: the Cobertura files it reads, where its report goes (null for none), which COVERAGE.md
// section that report replaces, and optionally the component each file's figures are reported as
[DebuggerDisplay("Files={Files.Count}, ReportPath={ReportPath}, Language={Language.Heading}")]
public sealed record JobCoverage(
    IReadOnlyList<string> Files,
    string? ReportPath,
    CoverageLanguage Language,
    IReadOnlyDictionary<string, string>? Components
);
