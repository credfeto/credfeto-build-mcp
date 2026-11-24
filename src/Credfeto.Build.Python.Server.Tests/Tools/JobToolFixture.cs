using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Python.Server.Tests.Helpers;
using Credfeto.Build.Python.Server.Tools;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Testing;
using ModelContextProtocol.Protocol;

namespace Credfeto.Build.Python.Server.Tests.Tools;

// A repository on disk, git-initialised with .venv ignored, and a way to call one tool on it as it is now
internal sealed class JobToolFixture : IDisposable
{
    public JobToolFixture()
    {
        this.Repository = new();
        this.Runner = new();
    }

    public TemporaryDirectory Repository { get; }

    public RecordingJobRunner Runner { get; }

    public void Dispose()
    {
        this.Repository.Dispose();
    }

    public ValueTask WriteAsync(string relative, string content, CancellationToken cancellationToken)
    {
        return PythonRepositories.WriteAsync(
            repository: this.Repository,
            relative: relative,
            content: content,
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask<CallToolResult> CallAsync(
        string tool,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken
    )
    {
        PythonDetection detection = await PythonDetector.DetectAsync(
            repositoryRoot: this.Repository.Path,
            cancellationToken: cancellationToken
        );
        ToolContext context = new(
            detection: new LiveDetectionSource(this.Repository.Path),
            runner: this.Runner,
            commands: TestTools.VersionCommands(),
            python: TestTools.FakeTools
        );
        BuildTool found = TestTools.Find(
            tools: PythonToolCatalog.Create(detection: detection, context: context),
            name: tool
        );

        return await found.CallAsync(arguments: arguments, cancellationToken: cancellationToken);
    }

    public IReadOnlyList<string> StepNames()
    {
        JobDefinition job = this.Runner.Started.Single();

        return [.. job.Steps.Select(static step => step.Name)];
    }
}
