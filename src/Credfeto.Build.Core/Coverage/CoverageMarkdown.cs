using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Credfeto.Build.Core.Coverage;

public static class CoverageMarkdown
{
    public static string Format(CoverageSummary summary, string? headSha)
    {
        StringBuilder builder = new();
        builder.Append("| Component | Line coverage |\n").Append("|---|---|\n");

        foreach (AssemblyCoverage assembly in summary.Assemblies)
        {
            AppendRow(builder: builder, name: assembly.Name, coverage: assembly);
        }

        AppendRow(builder: builder, name: "**" + summary.Overall.Name + "**", coverage: summary.Overall);

        if (!string.IsNullOrEmpty(headSha))
        {
            builder.Append("\nCaptured at commit `").Append(headSha).Append("`.\n");
        }

        return builder.ToString();
    }

    public static string FormatSection(CoverageSummary summary, CoverageLanguage language)
    {
        return CoverageTable.Render(
            language: language,
            rows:
            [
                .. summary.Assemblies.Select(static assembly =>
                    CoverageRow.Measured(
                        name: assembly.Name,
                        covered: assembly.LinesCovered,
                        coverable: assembly.LinesValid
                    )
                ),
            ],
            overall: CoverageRow.Measured(
                name: language.OverallLabel,
                covered: summary.Overall.LinesCovered,
                coverable: summary.Overall.LinesValid
            )
        );
    }

    // Rounded once from the line counts, so a figure already rounded to two places is never rounded twice
    internal static string OneDecimalPlace(int covered, int coverable)
    {
        decimal percentage = coverable == 0 ? 0m : covered * 100m / coverable;

        return decimal.Round(d: percentage, decimals: 1, mode: MidpointRounding.AwayFromZero)
            .ToString(format: "0.0", provider: CultureInfo.InvariantCulture);
    }

    private static void AppendRow(StringBuilder builder, string name, AssemblyCoverage coverage)
    {
        builder
            .Append("| ")
            .Append(name)
            .Append(" | ")
            .Append(coverage.LineCoverage.ToString(format: "0.0", provider: CultureInfo.InvariantCulture))
            .Append("% |\n");
    }
}
