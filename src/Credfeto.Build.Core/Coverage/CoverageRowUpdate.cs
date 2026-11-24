using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

// What replacing one component's row of COVERAGE.md did: the new file text (null when nothing was written) and the
// rows whose missing line counts kept Overall from being summed
[DebuggerDisplay("Outcome={Outcome}, RowsWithoutCounts={RowsWithoutCounts.Count}")]
public sealed record CoverageRowUpdate(
    CoverageRowOutcome Outcome,
    IReadOnlyList<string> RowsWithoutCounts,
    string? Text
)
{
    public static CoverageRowUpdate NoTable { get; } =
        new(Outcome: CoverageRowOutcome.NoTable, RowsWithoutCounts: [], Text: null);
}
