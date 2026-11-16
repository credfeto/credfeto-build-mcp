using System;
using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Build.Core.Coverage;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("Kind={Kind}, Steps={Steps.Count}, Timeout={Timeout}")]
public sealed record JobDefinition(
    string Kind,
    IReadOnlyList<ProcessStep> Steps,
    TimeSpan Timeout,
    bool ContinueOnFailure,
    ITestOutputParser? TestParser,
    IReadOnlyList<string> CoverageFiles,
    string? HeadSha
)
{
    public IChangedFilesProbe? ChangedFiles { get; init; }

    public string? CoverageReportPath { get; init; }

    // The COVERAGE.md section a coverage report replaces
    public CoverageLanguage CoverageLanguage { get; init; } = CoverageLanguage.Dotnet;
}
