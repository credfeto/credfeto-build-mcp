using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Credfeto.Build.Core.Coverage;

[DebuggerDisplay("Packages={Packages.Count}")]
public sealed record CoberturaReport(IReadOnlyList<CoberturaPackage> Packages)
{
    // The whole report as one component: a Node package's report splits its files into a Cobertura package per
    // directory, but COVERAGE.md has one row per npm package. A line hit in any of them counts as hit
    public CoberturaReport AsComponent(string name)
    {
        Dictionary<SourceLine, bool> lines = [];

        foreach ((SourceLine line, bool hit) in this.Packages.SelectMany(static package => package.Lines))
        {
            lines[line] = lines.TryGetValue(key: line, value: out bool existing) && existing || hit;
        }

        return new([new CoberturaPackage(Name: name, Lines: lines)]);
    }
}
