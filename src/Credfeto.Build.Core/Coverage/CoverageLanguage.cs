using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

// One language's section of COVERAGE.md: its heading, what a row measures and the label of its overall row
[DebuggerDisplay("{Heading}")]
public sealed record CoverageLanguage(string Heading, string ComponentColumn, string OverallLabel)
{
    // The documented order of COVERAGE.md's language sections; a new section is inserted where this order puts it
    private static readonly IReadOnlyList<string> SectionOrder = [".NET", "Node", "Python", "Shell"];

    public static CoverageLanguage Dotnet { get; } =
        new(Heading: ".NET", ComponentColumn: "Project", OverallLabel: "Overall (.NET)");

    public static CoverageLanguage Node { get; } =
        new(Heading: "Node", ComponentColumn: "Package", OverallLabel: "Overall (Node)");

    public static CoverageLanguage Python { get; } =
        new(Heading: "Python", ComponentColumn: "Package", OverallLabel: "Overall (Python)");

    // Where a section with this heading comes in the documented order; a heading the order does not list comes last
    public static int OrderOf(string heading)
    {
        for (int index = 0; index < SectionOrder.Count; index++)
        {
            if (StringComparer.Ordinal.Equals(x: SectionOrder[index], y: heading))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
