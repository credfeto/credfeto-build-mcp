using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Coverage;

public sealed class CoverageAggregatorTests : TestBase
{
    [Fact]
    public async Task OneReportGivesPerAssemblyAndOverallFiguresAsync()
    {
        CoverageSummary summary = CoverageAggregator.Combine([await this.ParseAsync(CoberturaSamples.ONE_PACKAGE)]);

        AssemblyCoverage core = Assert.Single(summary.Assemblies);
        Assert.Equal(expected: new("Example.Core", 3, 5, 60d), actual: core);
        Assert.Equal(expected: new(CoverageAggregator.OVERALL, 3, 5, 60d), actual: summary.Overall);
    }

    [Fact]
    public async Task ReportsAreMergedByAssemblyAndLineAsync()
    {
        IReadOnlyList<CoberturaReport> reports =
        [
            await this.ParseAsync(CoberturaSamples.ONE_PACKAGE),
            await this.ParseAsync(CoberturaSamples.OTHER_PACKAGE),
        ];

        CoverageSummary summary = CoverageAggregator.Combine(reports);

        // Alpha.cs line 11 was missed in the first report and hit in the second, so it counts as covered once
        Assert.Equal(
            expected: [new("Example.Core", 4, 5, 80d), new("Example.Other", 3, 4, 75d)],
            actual: summary.Assemblies
        );
        Assert.Equal(expected: new(CoverageAggregator.OVERALL, 7, 9, 77.78d), actual: summary.Overall);
    }

    [Fact]
    public async Task AVitestReportAsOneComponentIsOneRowAsync()
    {
        CoberturaReport report = await this.ParseAsync(CoberturaSamples.VITEST);

        CoverageSummary summary = CoverageAggregator.Combine([report.AsComponent("@example/web")]);

        // Its two directory packages (main and src.util) become the one npm package's row
        Assert.Equal(expected: new("@example/web", 4, 5, 80d), actual: Assert.Single(summary.Assemblies));
        Assert.Equal(expected: new(CoverageAggregator.OVERALL, 4, 5, 80d), actual: summary.Overall);
    }

    [Fact]
    public static void NoReportsGiveZeroOverall()
    {
        CoverageSummary summary = CoverageAggregator.Combine([]);

        Assert.Empty(summary.Assemblies);
        Assert.Equal(expected: new(CoverageAggregator.OVERALL, 0, 0, 0d), actual: summary.Overall);
    }

    [Fact]
    public static void MarkdownHasARowPerAssemblyOverallAndTheSha()
    {
        CoverageSummary summary = new(
            Assemblies: [new("Example.Core", 4, 5, 80d)],
            Overall: new(CoverageAggregator.OVERALL, 4, 5, 80d)
        );

        string markdown = CoverageMarkdown.Format(
            summary: summary,
            headSha: "0123456789abcdef0123456789abcdef01234567"
        );

        Assert.Equal(
            expected: "| Component | Line coverage |\n|---|---|\n| Example.Core | 80.0% |\n| **Overall** | 80.0% |\n\nCaptured at commit `0123456789abcdef0123456789abcdef01234567`.\n",
            actual: markdown
        );
    }

    [Fact]
    public static void MarkdownWithoutShaHasNoCommitLine()
    {
        CoverageSummary summary = new(Assemblies: [], Overall: new(CoverageAggregator.OVERALL, 0, 0, 0d));

        Assert.DoesNotContain(
            expectedSubstring: "Captured",
            actualString: CoverageMarkdown.Format(summary: summary, headSha: null),
            comparisonType: System.StringComparison.Ordinal
        );
    }

    [Fact]
    public static void SectionHasARowPerProjectAndTheOverallToOneDecimalPlace()
    {
        CoverageSummary summary = new(
            Assemblies: [new("Example.Core", 4, 5, 80d), new("Example.Other", 2, 3, 66.67d)],
            Overall: new(CoverageAggregator.OVERALL, 6, 8, 75d)
        );

        Assert.Equal(
            expected: "## .NET\n\n| Project | Line Coverage | Covered | Coverable |\n| --- | --- | --- | --- |\n| Example.Core | 80.0% | 4 | 5 |\n| Example.Other | 66.7% | 2 | 3 |\n| **Overall (.NET)** | **75.0%** | **6** | **8** |\n",
            actual: CoverageMarkdown.FormatSection(summary: summary, language: CoverageLanguage.Dotnet)
        );
    }

    [Fact]
    public static void SectionRoundsOnceFromTheLineCounts()
    {
        // 50,449 of 100,000 lines is 50.449%, so 50.4%; rounding the two-place 50.45% again would give 50.5%
        CoverageSummary summary = new(Assemblies: [], Overall: new(CoverageAggregator.OVERALL, 50449, 100000, 50.45d));

        Assert.EndsWith(
            expectedEndString: "| **Overall (.NET)** | **50.4%** | **50449** | **100000** |\n",
            actualString: CoverageMarkdown.FormatSection(summary: summary, language: CoverageLanguage.Dotnet),
            comparisonType: System.StringComparison.Ordinal
        );
    }

    [Fact]
    public static void SectionOfNoLinesIsZero()
    {
        CoverageSummary summary = new(Assemblies: [], Overall: new(CoverageAggregator.OVERALL, 0, 0, 0d));

        Assert.EndsWith(
            expectedEndString: "| **Overall (.NET)** | **0.0%** | **0** | **0** |\n",
            actualString: CoverageMarkdown.FormatSection(summary: summary, language: CoverageLanguage.Dotnet),
            comparisonType: System.StringComparison.Ordinal
        );
    }

    private async ValueTask<CoberturaReport> ParseAsync(string xml)
    {
        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));

        return await CoberturaParser.ParseAsync(stream: stream, cancellationToken: this.CancellationToken());
    }
}
