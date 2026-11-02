using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Json;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tools;

public static class JobStarter
{
    public static async ValueTask<string> StartAsync(
        IJobRunner runner,
        DotnetRepository repository,
        string kind,
        IReadOnlyList<ProcessStep> steps,
        ITestOutputParser? testParser,
        IReadOnlyList<string> coverageFiles,
        IChangedFilesProbe? changedFiles,
        CancellationToken cancellationToken
    )
    {
        string? headSha = await GitHead.TryReadAsync(repositoryRoot: repository.RepositoryRoot, cancellationToken: cancellationToken);

        JobDefinition definition = new(
            Kind: kind,
            Steps: steps,
            Timeout: repository.Timeout,
            ContinueOnFailure: testParser is not null,
            TestParser: testParser,
            CoverageFiles: coverageFiles,
            HeadSha: headSha
        )
        {
            ChangedFiles = changedFiles,
        };

        JobStatus status = await runner.StartAsync(definition);

        return JsonSerializer.Serialize(value: status, jsonTypeInfo: CoreJsonContext.Default.JobStatus);
    }
}
