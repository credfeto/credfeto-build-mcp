using System;
using System.Collections.Generic;
using System.Linq;

namespace Credfeto.Build.Core.Coverage;

public static class CoverageAggregator
{
    public const string OVERALL = "Overall";

    public static CoverageSummary Combine(IEnumerable<CoberturaReport> reports)
    {
        Dictionary<string, Dictionary<SourceLine, bool>> merged = [with(StringComparer.Ordinal)];

        foreach (CoberturaPackage package in reports.SelectMany(static report => report.Packages))
        {
            if (!merged.TryGetValue(key: package.Name, value: out Dictionary<SourceLine, bool>? lines))
            {
                lines = [];
                merged.Add(key: package.Name, value: lines);
            }

            foreach ((SourceLine line, bool hit) in package.Lines)
            {
                lines[line] = lines.TryGetValue(key: line, value: out bool existing) && existing || hit;
            }
        }

        AssemblyCoverage[] assemblies =
        [
            .. merged
                .OrderBy(keySelector: static pair => pair.Key, comparer: StringComparer.Ordinal)
                .Select(static pair => Measure(name: pair.Key, lines: pair.Value)),
        ];

        int covered = assemblies.Sum(static assembly => assembly.LinesCovered);
        int valid = assemblies.Sum(static assembly => assembly.LinesValid);

        return new(Assemblies: assemblies, Overall: Create(name: OVERALL, covered: covered, valid: valid));
    }

    private static AssemblyCoverage Measure(string name, Dictionary<SourceLine, bool> lines)
    {
        int covered = lines.Count(static pair => pair.Value);

        return Create(name: name, covered: covered, valid: lines.Count);
    }

    private static AssemblyCoverage Create(string name, int covered, int valid)
    {
        double rate = valid == 0 ? 0d : Math.Round(value: covered * 100d / valid, digits: 2, mode: MidpointRounding.AwayFromZero);

        return new(Name: name, LinesCovered: covered, LinesValid: valid, LineCoverage: rate);
    }
}
