using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Server.Tools;

public static class JobStarter
{
    public static ValueTask<string> StartAsync(
        IJobRunner runner,
        string repositoryRoot,
        TimeSpan timeout,
        string kind,
        IReadOnlyList<ProcessStep> steps,
        ITestOutputParser? testParser,
        IReadOnlyList<string> coverageFiles,
        IChangedFilesProbe? changedFiles,
        CancellationToken cancellationToken
    )
    {
        return StartAsync(
            runner: runner,
            repositoryRoot: repositoryRoot,
            timeout: timeout,
            kind: kind,
            steps: steps,
            testParser: testParser,
            coverageFiles: coverageFiles,
            changedFiles: changedFiles,
            coverageReportPath: null,
            cancellationToken: cancellationToken
        );
    }

    public static ValueTask<string> StartAsync(
        IJobRunner runner,
        string repositoryRoot,
        TimeSpan timeout,
        string kind,
        IReadOnlyList<ProcessStep> steps,
        ITestOutputParser? testParser,
        IReadOnlyList<string> coverageFiles,
        IChangedFilesProbe? changedFiles,
        string? coverageReportPath,
        CancellationToken cancellationToken
    )
    {
        return StartAsync(
            runner: runner,
            repositoryRoot: repositoryRoot,
            timeout: timeout,
            kind: kind,
            steps: steps,
            testParser: testParser,
            coverage: new(
                Files: coverageFiles,
                ReportPath: coverageReportPath,
                Language: CoverageLanguage.Dotnet,
                Components: null
            ),
            changedFiles: changedFiles,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask<string> StartAsync(
        IJobRunner runner,
        string repositoryRoot,
        TimeSpan timeout,
        string kind,
        IReadOnlyList<ProcessStep> steps,
        ITestOutputParser? testParser,
        JobCoverage coverage,
        IChangedFilesProbe? changedFiles,
        CancellationToken cancellationToken
    )
    {
        string? headSha = await GitHead.TryReadAsync(
            repositoryRoot: repositoryRoot,
            cancellationToken: cancellationToken
        );

        JobDefinition definition = new(
            Kind: kind,
            Steps: steps,
            Timeout: timeout,
            ContinueOnFailure: testParser is not null,
            TestParser: testParser,
            CoverageFiles: coverage.Files,
            HeadSha: headSha
        )
        {
            ChangedFiles = changedFiles,
            CoverageReportPath = coverage.ReportPath,
            CoverageLanguage = coverage.Language,
            CoverageComponents = coverage.Components,
        };

        JobStatus status = await runner.StartAsync(definition);

        return JsonSerializer.Serialize(value: status, jsonTypeInfo: CoreJsonContext.Default.JobStatus);
    }
}
