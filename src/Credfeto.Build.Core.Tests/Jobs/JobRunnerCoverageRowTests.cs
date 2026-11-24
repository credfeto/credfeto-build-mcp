using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Services;
using Credfeto.Build.Core.Tests.Coverage;
using Credfeto.Build.Core.Tests.Helpers;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Build.Core.Tests.Jobs;

// A run of one project or package replaces only its own row of COVERAGE.md and sums Overall again from the line counts
public sealed class JobRunnerCoverageRowTests : TestBase, IDisposable
{
    private const string CORE = "Example.Core";

    private const string OTHER_ONLY = """
        <coverage>
          <packages>
            <package name="Example.Other">
              <classes>
                <class name="Example.Other.Gamma" filename="/src/Example.Other/Gamma.cs">
                  <lines>
                    <line number="1" hits="1" />
                    <line number="2" hits="1" />
                    <line number="3" hits="1" />
                    <line number="4" hits="0" />
                  </lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    private readonly ScriptedProcessLauncher _launcher = new();
    private readonly TemporaryDirectory _logs = new();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(year: 2026, month: 11, day: 24, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero)
    );

    public void Dispose()
    {
        this._logs.Dispose();
    }

    [Fact]
    public async Task AFullRunWritesEveryRowsCountsAndOverallIsTheirSumAsync()
    {
        string report = this._logs.Combine("COVERAGE.md");

        JobResult result = await this.RunAsync(await this.FullRunAsync(report));

        Assert.Empty(result.CoverageErrors);
        Assert.Null(result.CoverageReportNote);
        Assert.Contains(
            expectedSubstring: "| Example.Core | 60.0% | 3 | 5 |\n| Example.Other | 75.0% | 3 | 4 |\n| **Overall (.NET)** | **66.7%** | **6** | **9** |\n",
            actualString: await this.ReadAsync(report),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ASingleProjectRunMatchesAFullRunAsync()
    {
        string report = this._logs.Combine("COVERAGE.md");
        _ = await this.RunAsync(await this.FullRunAsync(report));
        string fullRun = await this.ReadAsync(report);

        // An earlier figure for the project, and an Overall that no longer adds up, both replaced by the run
        await this.WriteAsync(
            report: report,
            content: fullRun
                .Replace(
                    oldValue: "| Example.Core | 60.0% | 3 | 5 |",
                    newValue: "| Example.Core | 20.0% | 1 | 5 |",
                    comparisonType: StringComparison.Ordinal
                )
                .Replace(oldValue: "**66.7%**", newValue: "**44.4%**", comparisonType: StringComparison.Ordinal)
        );

        JobResult result = await this.RunAsync(await this.SingleRunAsync(report: report, component: CORE));

        Assert.Empty(result.CoverageErrors);
        Assert.Equal(expected: report, actual: result.CoverageReportPath);
        Assert.Equal(
            expected: "Updated the Example.Core row and recomputed Overall (.NET) from the line counts of every row",
            actual: result.CoverageReportNote
        );
        Assert.Equal(expected: fullRun, actual: await this.ReadAsync(report));
    }

    [Fact]
    public async Task AFileWithoutCountsKeepsItsOverallAndTheResultSaysWhyAsync()
    {
        string report = this._logs.Combine("COVERAGE.md");
        await this.WriteAsync(
            report: report,
            content: "# Coverage\n\n## .NET\n\n| Component | Line coverage |\n| --- | --- |\n| Example.Core | 20.00% |\n| Example.Other | 75.00% |\n| **Overall** | **44.44%** |\n\n---\n\nCaptured at commit `old` on 2026-01-01.\n"
        );

        JobResult result = await this.RunAsync(await this.SingleRunAsync(report: report, component: CORE));

        Assert.Empty(result.CoverageErrors);
        Assert.Equal(expected: report, actual: result.CoverageReportPath);
        Assert.Equal(
            expected: "Updated the Example.Core row but left Overall (.NET) unchanged, because these rows have no line counts to sum (they were written before COVERAGE.md recorded them): Example.Other. The next full coverage run fills them in",
            actual: result.CoverageReportNote
        );
        Assert.Contains(
            expectedSubstring: "| Example.Core | 60.0% | 3 | 5 |\n| Example.Other | 75.00% | | |\n| **Overall (.NET)** | **44.44%** | | |\n",
            actualString: await this.ReadAsync(report),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ARunThatMeasuredNothingOfItsComponentIsNotWrittenAsync()
    {
        string report = this._logs.Combine("COVERAGE.md");
        await this.WriteAsync(report: report, content: "unchanged");

        JobResult result = await this.RunAsync(await this.SingleRunAsync(report: report, component: "Example.Missing"));

        Assert.Null(result.CoverageReportPath);
        Assert.Equal(
            expected: ["COVERAGE.md was not written because the run measured no lines of Example.Missing"],
            actual: result.CoverageErrors,
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "unchanged", actual: await this.ReadAsync(report));
    }

    [Fact]
    public async Task ASingleProjectRunNeverCreatesTheFileAsync()
    {
        string report = this._logs.Combine("COVERAGE.md");

        JobResult result = await this.RunAsync(await this.SingleRunAsync(report: report, component: CORE));

        Assert.Null(result.CoverageReportPath);
        Assert.Equal(
            expected:
            [
                "COVERAGE.md was not written because it has no .NET coverage table to update a row of; a full coverage run writes the whole section",
            ],
            actual: result.CoverageErrors,
            comparer: StringComparer.Ordinal
        );
        Assert.False(condition: File.Exists(report), userMessage: "only a full run writes a new file");
    }

    [Fact]
    public async Task ASinglePackageRunUpdatesItsNodeRowAndOverallAsync()
    {
        const string table = "| Package | Line Coverage | Covered | Coverable |\n| --- | --- | --- | --- |\n";
        string report = this._logs.Combine("COVERAGE.md");
        await this.WriteAsync(
            report: report,
            content: "# Coverage\n\n## Node\n\n"
                + table
                + "| @example/core | 50.0% | 1 | 2 |\n| @example/web | 0.0% | 0 | 5 |\n| **Overall (Node)** | **14.3%** | **1** | **7** |\n\n---\n\nCaptured at commit `old` on 2026-01-01.\n"
        );
        string web = await this._logs.WriteAsync(
            relative: "packages/web/coverage/cobertura-coverage.xml",
            content: CoberturaSamples.VITEST,
            cancellationToken: this.CancellationToken()
        );

        JobResult result = await this.RunAsync(
            Definition(report) with
            {
                CoverageFiles = [web],
                CoverageLanguage = CoverageLanguage.Node,
                CoverageComponents = new Dictionary<string, string>(StringComparer.Ordinal) { [web] = "@example/web" },
                CoverageReportComponent = "@example/web",
            }
        );

        Assert.Empty(result.CoverageErrors);
        Assert.Equal(
            expected: "# Coverage\n\n## Node\n\n"
                + table
                + "| @example/core | 50.0% | 1 | 2 |\n| @example/web | 80.0% | 4 | 5 |\n| **Overall (Node)** | **71.4%** | **5** | **7** |\n\n---\n\nCaptured at commit `abc` on 2026-11-24.\n",
            actual: await this.ReadAsync(report)
        );
    }

    private async ValueTask<JobDefinition> FullRunAsync(string report)
    {
        string core = await this.CoberturaAsync(name: CORE, content: CoberturaSamples.ONE_PACKAGE);
        string other = await this.CoberturaAsync(name: "Example.Other", content: OTHER_ONLY);

        return Definition(report) with
        {
            CoverageFiles = [core, other],
        };
    }

    private async ValueTask<JobDefinition> SingleRunAsync(string report, string component)
    {
        string core = await this.CoberturaAsync(name: CORE, content: CoberturaSamples.ONE_PACKAGE);

        return Definition(report) with
        {
            CoverageFiles = [core],
            CoverageReportComponent = component,
        };
    }

    private ValueTask<string> CoberturaAsync(string name, string content)
    {
        return this._logs.WriteAsync(
            relative: "coverage/" + name + ".coverage.cobertura.xml",
            content: content,
            cancellationToken: this.CancellationToken()
        );
    }

    private async ValueTask WriteAsync(string report, string content)
    {
        await File.WriteAllTextAsync(path: report, contents: content, cancellationToken: this.CancellationToken());
    }

    private Task<string> ReadAsync(string report)
    {
        return File.ReadAllTextAsync(path: report, cancellationToken: this.CancellationToken());
    }

    private async ValueTask<JobResult> RunAsync(JobDefinition definition)
    {
        await using JobRunner runner = new(
            launcher: this._launcher,
            masker: new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal)),
            settings: JobRunnerSettings.Create(this._logs.Path),
            timeProvider: this._time
        );

        JobStatus status = await runner.StartAsync(definition);
        bool finished = await runner.WaitForCompletionAsync(
            id: status.Id,
            maxWait: Timeout.InfiniteTimeSpan,
            cancellationToken: this.CancellationToken()
        );
        Assert.True(condition: finished, userMessage: "An unbounded wait should only return once the job has finished");

        return runner.FindResult(status.Id) ?? throw new InvalidOperationException("The job vanished");
    }

    private static JobDefinition Definition(string report)
    {
        return new(
            Kind: "coverage",
            Steps:
            [
                new(
                    Name: "a",
                    FileName: "dotnet",
                    Arguments: ["a"],
                    WorkingDirectory: "/",
                    Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                ),
            ],
            Timeout: TimeSpan.FromMinutes(5),
            ContinueOnFailure: true,
            TestParser: null,
            CoverageFiles: [],
            HeadSha: "abc"
        )
        {
            CoverageReportPath = report,
        };
    }
}
