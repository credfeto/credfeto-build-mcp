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
