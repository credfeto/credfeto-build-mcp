using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Exceptions;
using Credfeto.Build.Core.Jobs.Services;
using Credfeto.Build.Core.Tests.Coverage;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Credfeto.Build.Core.Tests.Jobs;

public sealed class JobRunnerTests : TestBase, IDisposable
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(5);

    private readonly ScriptedProcessLauncher _launcher = new();
    private readonly TemporaryDirectory _logs = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose()
    {
        this._logs.Dispose();
    }

    [Fact]
    public async Task SuccessfulJobRecordsStepsTestsTailAndLogAsync()
    {
        this._launcher.Prints(step: "a", exitCode: 0, "pass one", "pass two").Prints(step: "b", exitCode: 0, "fail three");

        await using JobRunner runner = this.CreateRunner();
        JobResult result = await this.RunAsync(runner: runner, Definition(continueOnFailure: true, parser: new LineCountingParser(), "a", "b"));

        // The state follows the exit codes; the parser only reports what the output said
        Assert.Equal(expected: JobState.Succeeded, actual: result.State);
        Assert.Equal(expected: ["a", "b"], actual: [result.Steps[0].Name, result.Steps[1].Name], comparer: StringComparer.Ordinal);
        Assert.Equal(expected: new(Total: 3, Passed: 2, Failed: 1, Skipped: 0), actual: result.Tests);
        Assert.Equal(expected: ["three"], actual: result.FailedTests, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: "pass two", collection: result.OutputTail, comparer: StringComparer.Ordinal);

        string log = await File.ReadAllTextAsync(path: result.LogPath, cancellationToken: this.CancellationToken());
        Assert.Contains(expectedSubstring: "$ dotnet a --flag", actualString: log, comparisonType: StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "[exit code 0]", actualString: log, comparisonType: StringComparison.Ordinal);
        Assert.StartsWith(expectedStartString: this._logs.Path, actualString: result.LogPath, comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task StepsAreRunAsAFixedArgumentVectorAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        _ = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));

        ProcessStep started = Assert.Single(this._launcher.Started);
        Assert.Equal(expected: "dotnet", actual: started.FileName);
        Assert.Equal(expected: ["a", "--flag"], actual: started.Arguments, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task FailedStepStopsTheJobUnlessItContinuesOnFailureAsync()
    {
        this._launcher.Prints(step: "a", exitCode: 1);

        await using JobRunner runner = this.CreateRunner();
        JobResult result = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a", "b"));

        Assert.Equal(expected: JobState.Failed, actual: result.State);
        StepResult step = Assert.Single(result.Steps);
        Assert.Equal(expected: 1, actual: step.ExitCode);
    }

    [Fact]
    public async Task AllStepsSucceedingSucceedsAsync()
    {
        await using JobRunner runner = this.CreateRunner();
        JobResult result = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a", "b"));

        Assert.Equal(expected: JobState.Succeeded, actual: result.State);
        Assert.Equal(expected: 2, actual: result.Steps.Count);
        Assert.Null(result.Tests);
    }

    [Fact]
    public async Task SecretsAreMaskedInTheLogAndTailAsync()
    {
        this._launcher.Prints(step: "a", exitCode: 0, "using ghp_abcdefghijklmnopqrstuvwxyz0123");

        await using JobRunner runner = this.CreateRunner();
        JobResult result = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));

        string log = await File.ReadAllTextAsync(path: result.LogPath, cancellationToken: this.CancellationToken());
        Assert.DoesNotContain(expectedSubstring: "ghp_", actualString: log, comparisonType: StringComparison.Ordinal);
        Assert.Contains(expected: "using ***", collection: result.OutputTail, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task OutputIsCappedAndAnsiStrippedAsync()
    {
        this._launcher.Prints(step: "a", exitCode: 0, "\u001b[32mgreen\u001b[0m", "0123456789abcdefghijKLMNO", "x", "y", "z", "last");

        await using JobRunner runner = this.CreateRunner(
            JobRunnerSettings.Create(this._logs.Path) with { MaxLogCharacters = 70, MaxLineLength = 20, TailLines = 3 }
        );
        JobResult result = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));

        Assert.Equal(expected: ["z", "last", "[exit code 0]"], actual: result.OutputTail, comparer: StringComparer.Ordinal);

        string[] log = await File.ReadAllLinesAsync(path: result.LogPath, cancellationToken: this.CancellationToken());
        Assert.Equal(expected: "green", actual: log[1]);
        Assert.Equal(expected: "0123456789abcdefghij [line truncated]", actual: log[2]);
        Assert.StartsWith(expectedStartString: "[log truncated", actualString: log[^1], comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimeoutKillsTheJobAsync()
    {
        this._launcher.Hangs("a");

        await using JobRunner runner = this.CreateRunner();
        JobStatus started = await runner.StartAsync(Definition(continueOnFailure: false, parser: null, "a", "b"));
        await this._launcher.WaitForHangAsync(this.CancellationToken());

        this._time.Advance(JobTimeout + TimeSpan.FromSeconds(1));
        await runner.WaitForCompletionAsync(id: started.Id, cancellationToken: this.CancellationToken());

        JobResult? result = runner.FindResult(started.Id);
        Assert.NotNull(result);
        Assert.Equal(expected: JobState.TimedOut, actual: result.State);
        Assert.Contains(expectedSubstring: "timeout", actualString: result.Message, comparisonType: StringComparison.Ordinal);
        Assert.Single(this._launcher.Started);
    }

    [Fact]
    public async Task OnlyOneJobRunsAtATimeAndShutdownCancelsItAsync()
    {
        this._launcher.Hangs("a");

        JobRunner runner = this.CreateRunner();
        JobStatus started = await runner.StartAsync(Definition(continueOnFailure: false, parser: null, "a"));
        await this._launcher.WaitForHangAsync(this.CancellationToken());

        Assert.Equal(expected: JobState.Running, actual: runner.FindStatus(started.Id)?.State);
        Assert.Equal(expected: "a", actual: runner.FindStatus(started.Id)?.CurrentStep);
        await Assert.ThrowsAsync<JobAlreadyRunningException>(() => runner.StartAsync(Definition(continueOnFailure: false, parser: null, "b")).AsTask());

        await runner.DisposeAsync();

        Assert.Equal(expected: JobState.Cancelled, actual: runner.FindStatus(started.Id)?.State);
    }

    [Fact]
    public async Task UnknownJobIsNotFoundAsync()
    {
        await using JobRunner runner = this.CreateRunner();

        Assert.Null(runner.FindStatus("0123456789abcdef0123456789abcdef"));
        Assert.Null(runner.FindResult("0123456789abcdef0123456789abcdef"));
    }

    [Fact]
    public async Task ProcessThatCannotStartFailsTheJobAsync()
    {
        await using JobRunner runner = new(
            launcher: new ThrowingLauncher(),
            masker: new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal)),
            settings: JobRunnerSettings.Create(this._logs.Path),
            timeProvider: this._time
        );

        JobResult result = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));

        Assert.Equal(expected: JobState.Failed, actual: result.State);
        Assert.Contains(expectedSubstring: "could not be started", actualString: result.Message, comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task CoverageFilesAreParsedAndMissingOnesReportedAsync()
    {
        string present = await this._logs.WriteAsync(relative: "coverage/Example.Core.coverage.cobertura.xml", content: CoberturaSamples.ONE_PACKAGE, cancellationToken: this.CancellationToken());
        string missing = this._logs.Combine("coverage/Example.Other.coverage.cobertura.xml");

        await using JobRunner runner = this.CreateRunner();
        JobResult result = await this.RunAsync(
            runner: runner,
            Definition(continueOnFailure: true, parser: null, "a") with { CoverageFiles = [present, missing], HeadSha = "abc" }
        );

        Assert.NotNull(result.Coverage);
        Assert.Equal(expected: 60d, actual: result.Coverage.Overall.LineCoverage);
        Assert.Equal(expected: ["No coverage file was produced: Example.Other.coverage.cobertura.xml"], actual: result.CoverageErrors, comparer: StringComparer.Ordinal);
        Assert.Contains(expectedSubstring: "Captured at commit `abc`", actualString: result.CoverageMarkdown, comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task OldestFinishedJobsAreEvictedAsync()
    {
        await using JobRunner runner = this.CreateRunner(JobRunnerSettings.Create(this._logs.Path) with { MaxRetainedJobs = 2 });

        JobResult first = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));
        JobResult second = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));
        JobResult third = await this.RunAsync(runner: runner, Definition(continueOnFailure: false, parser: null, "a"));

        Assert.Null(runner.FindStatus(first.Id));
        Assert.NotNull(runner.FindStatus(second.Id));
        Assert.NotNull(runner.FindStatus(third.Id));
    }

    private JobRunner CreateRunner(JobRunnerSettings? settings = null)
    {
        return new(
            launcher: this._launcher,
            masker: new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal)),
            settings: settings ?? JobRunnerSettings.Create(this._logs.Path),
            timeProvider: this._time
        );
    }

    private async ValueTask<JobResult> RunAsync(JobRunner runner, JobDefinition definition)
    {
        JobStatus status = await runner.StartAsync(definition);
        await runner.WaitForCompletionAsync(id: status.Id, cancellationToken: this.CancellationToken());

        return runner.FindResult(status.Id) ?? throw new InvalidOperationException("The job vanished");
    }

    private static JobDefinition Definition(bool continueOnFailure, ITestOutputParser? parser, params string[] steps)
    {
        List<ProcessStep> processSteps = [];

        foreach (string step in steps)
        {
            processSteps.Add(
                new(
                    Name: step,
                    FileName: "dotnet",
                    Arguments: [step, "--flag"],
                    WorkingDirectory: "/",
                    Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                )
            );
        }

        return new(
            Kind: "test",
            Steps: processSteps,
            Timeout: JobTimeout,
            ContinueOnFailure: continueOnFailure,
            TestParser: parser,
            CoverageFiles: [],
            HeadSha: null
        );
    }

    private sealed class ThrowingLauncher : IProcessLauncher
    {
        public ValueTask<int> RunAsync(ProcessStep step, Func<string, ValueTask> onOutputLine, CancellationToken cancellationToken)
        {
            throw new Win32Exception("No such file or directory");
        }
    }
}
