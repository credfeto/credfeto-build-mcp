using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Credfeto.Build.Core.Coverage;

// The table of one language's COVERAGE.md section. The percentage stays the second column, where the coverage ratchet
// reads the Overall figure; the covered and coverable line counts follow it so one component's row can be replaced and
// Overall summed again without measuring every other component
public static class CoverageTable
{
    private const string NOT_APPLICABLE = "n/a";

    private static readonly char[] CellSeparator = ['|'];

    public static string Render(CoverageLanguage language, IReadOnlyList<CoverageRow> rows, CoverageRow overall)
    {
        StringBuilder builder = new();
        builder
            .Append("## ")
            .Append(language.Heading)
            .Append("\n\n| ")
            .Append(language.ComponentColumn)
            .Append(" | Line Coverage | Covered | Coverable |\n")
            .Append("| --- | --- | --- | --- |\n");

        foreach (CoverageRow row in rows)
        {
            AppendRow(builder: builder, row: row, bold: false);
        }

        AppendRow(builder: builder, row: overall with { Name = language.OverallLabel }, bold: true);

        return builder.ToString();
    }

    // The component rows and the Overall row of a section's table; false when the section has no table with an
    // Overall row (n/a, excluded, or not a coverage table at all)
    public static bool TryParse(
        string section,
        out IReadOnlyList<CoverageRow> rows,
        [NotNullWhen(true)] out CoverageRow? overall
    )
    {
        string[] lines =
        [
            .. section
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Select(static line => line.Trim())
                .Where(static line => line is ['|', ..]),
        ];

        List<CoverageRow> components = [];
        overall = null;

        // The first two table lines are the header and its separator
        foreach (CoverageRow row in lines.Skip(2).Select(Parse).OfType<CoverageRow>())
        {
            if (row.Name.StartsWith(value: CoverageAggregator.OVERALL, comparisonType: StringComparison.Ordinal))
            {
                overall = row;
            }
            else
            {
                components.Add(row);
            }
        }

        rows = components;

        return overall is not null;
    }

    private static CoverageRow? Parse(string line)
    {
        string[] cells = [.. line.Trim('|').Split(CellSeparator).Select(static cell => Unbold(cell.Trim()))];

        if (cells.Length < 2 || cells[0].Length == 0)
        {
            return null;
        }

        int? covered = cells.Length >= 4 ? Count(cells[2]) : null;
        int? coverable = cells.Length >= 4 ? Count(cells[3]) : null;

        // A row with nothing to measure counts as no lines at all, so it never stops Overall being summed
        if (
            (covered is null || coverable is null)
            && cells[1].StartsWith(value: NOT_APPLICABLE, comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            return new(Name: cells[0], Percentage: cells[1], Covered: 0, Coverable: 0);
        }

        return new(Name: cells[0], Percentage: cells[1], Covered: covered, Coverable: coverable);
    }

    private static int? Count(string cell)
    {
        return int.TryParse(
            s: cell,
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out int count
        )
            ? count
            : null;
    }

    private static string Unbold(string cell)
    {
        return
            cell.Length >= 4
            && cell.StartsWith(value: "**", comparisonType: StringComparison.Ordinal)
            && cell.EndsWith(value: "**", comparisonType: StringComparison.Ordinal)
            ? cell[2..^2].Trim()
            : cell;
    }

    private static void AppendRow(StringBuilder builder, CoverageRow row, bool bold)
    {
        string emphasis = bold ? "**" : string.Empty;

        builder.Append("| ").Append(emphasis).Append(row.Name).Append(emphasis);
        AppendCell(builder: builder, value: row.Percentage, emphasis: emphasis);
        AppendCell(builder: builder, value: Format(row.Covered), emphasis: emphasis);
        AppendCell(builder: builder, value: Format(row.Coverable), emphasis: emphasis);
        builder.Append(" |\n");
    }

    private static void AppendCell(StringBuilder builder, string value, string emphasis)
    {
        // A row with no counts keeps its cells empty rather than inventing a figure
        if (value.Length == 0)
        {
            builder.Append(" |");

            return;
        }

        builder.Append(" | ").Append(emphasis).Append(value).Append(emphasis);
    }

    private static string Format(int? count)
    {
        return count?.ToString(provider: CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
