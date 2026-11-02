using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

/// <summary>
///     A job: process steps run in order, a timeout for the whole job, and what to collect when it finishes.
/// </summary>
[DebuggerDisplay("Kind={Kind}, Steps={Steps.Count}, Timeout={Timeout}")]
public sealed record JobDefinition(
    string Kind,
    IReadOnlyList<ProcessStep> Steps,
    TimeSpan Timeout,
    bool ContinueOnFailure,
    ITestOutputParser? TestParser,
    IReadOnlyList<string> CoverageFiles,
    string? HeadSha
);
