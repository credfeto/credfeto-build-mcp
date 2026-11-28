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

    // Told to the caller with the job's result, for what the tool knew when it started the job but could not act on
    public string? Note { get; init; }

    public string? CoverageReportPath { get; init; }

    // When set, the report replaces only this component's row of the section and sums Overall again from the line
    // counts of every row (a run of one project or package); when null, the run rewrites the whole section
    public string? CoverageReportComponent { get; init; }

    // The COVERAGE.md section a coverage report replaces
    public CoverageLanguage CoverageLanguage { get; init; } = CoverageLanguage.Dotnet;

    // When set, the component a coverage file's figures are reported as, keyed by the file's path; a file it does
    // not name keeps its own Cobertura package names
    public IReadOnlyDictionary<string, string>? CoverageComponents { get; init; }

    // The memory limit and worker count the tool chose the job's arguments by, reported with its result
    public ResourceNote? Resources { get; init; }
}
