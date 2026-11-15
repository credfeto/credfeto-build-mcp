using System;
using System.Globalization;
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

    public static string FormatSection(CoverageSummary summary)
    {
        StringBuilder builder = new();
        builder.Append("## .NET\n\n").Append("| Project | Line Coverage |\n").Append("| --- | --- |\n");

        foreach (AssemblyCoverage assembly in summary.Assemblies)
        {
            builder.Append("| ").Append(assembly.Name).Append(" | ").Append(OneDecimalPlace(assembly)).Append("% |\n");
        }

        builder.Append("| **Overall (.NET)** | **").Append(OneDecimalPlace(summary.Overall)).Append("%** |\n");

        return builder.ToString();
    }

    private static string OneDecimalPlace(AssemblyCoverage coverage)
    {
        // Rounded once from the line counts, so a figure already rounded to two places is never rounded twice
        decimal percentage = coverage.LinesValid == 0 ? 0m : coverage.LinesCovered * 100m / coverage.LinesValid;

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
