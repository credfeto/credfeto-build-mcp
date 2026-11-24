using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

// One row of a COVERAGE.md table: the percentage as written, and the line counts it came from when the file has them
// (a file written before the counts were recorded has none, so its rows cannot be summed)
[DebuggerDisplay("Name={Name}, Percentage={Percentage}, Covered={Covered}, Coverable={Coverable}")]
public sealed record CoverageRow(string Name, string Percentage, int? Covered, int? Coverable)
{
    public bool HasCounts => this.Covered is not null && this.Coverable is not null;

    public static CoverageRow Measured(string name, int covered, int coverable)
    {
        return new(
            Name: name,
            Percentage: CoverageMarkdown.OneDecimalPlace(covered: covered, coverable: coverable) + "%",
            Covered: covered,
            Coverable: coverable
        );
    }
}
