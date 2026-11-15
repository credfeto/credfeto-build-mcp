using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

    public static async ValueTask<string> StartAsync(
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
            CoverageFiles: coverageFiles,
            HeadSha: headSha
        )
        {
            ChangedFiles = changedFiles,
            CoverageReportPath = coverageReportPath,
        };

        JobStatus status = await runner.StartAsync(definition);

        return JsonSerializer.Serialize(value: status, jsonTypeInfo: CoreJsonContext.Default.JobStatus);
    }
}
