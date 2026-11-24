namespace Credfeto.Build.Core.Coverage;

public enum CoverageRowOutcome
{
    // The row was written and Overall summed again from every row's line counts
    OverallRecomputed,

    // The row was written, but some rows have no line counts, so Overall was left as it was
    OverallUnchanged,

    // The file has no table for the language to update, so nothing was written
    NoTable,
}
