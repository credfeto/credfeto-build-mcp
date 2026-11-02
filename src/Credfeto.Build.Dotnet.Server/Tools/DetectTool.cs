using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Json;
using Credfeto.Build.Dotnet.Server.Models;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class DetectTool : BuildTool
{
    private readonly DotnetDetection _detection;
    private readonly DotnetRepository _repository;

    public DetectTool(DotnetDetection detection, DotnetRepository repository)
        : base(
            name: ToolNames.DETECT,
            title: "Detect .NET",
            description: "Lists the solution, its projects and the unit test projects the test and coverage tools can run (*.Tests, excluding *.Integration.Tests and *.Benchmark.Tests), as found when the server started",
            schema: [],
            readOnly: true
        )
    {
        this._detection = detection;
        this._repository = repository;
    }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        DetectResponse response = new(
            RepositoryRoot: this._repository.RepositoryRoot,
            Solution: RepositoryPaths.RelativeTo(root: this._repository.RepositoryRoot, path: this._repository.SolutionPath),
            SolutionDirectory: RepositoryPaths.RelativeTo(root: this._repository.RepositoryRoot, path: this._repository.SolutionDirectory),
            Projects: [.. this._repository.Projects.Select(static project => project.RelativePath)],
            UnitTestProjects: [.. this._repository.UnitTestProjects.Select(static project => project.Name)],
            IntegrationTestProjects: this.NamesOf(DotnetProjectKind.IntegrationTests),
            BenchmarkTestProjects: this.NamesOf(DotnetProjectKind.BenchmarkTests),
            TimeoutMinutes: (int)this._repository.Timeout.TotalMinutes,
            OverridesError: this._detection.OverridesError
        );

        return ValueTask.FromResult(JsonSerializer.Serialize(value: response, jsonTypeInfo: DotnetJsonContext.Default.DetectResponse));
    }

    private string[] NamesOf(DotnetProjectKind kind)
    {
        return [.. this._repository.Projects.Where(project => project.Kind == kind).Select(static project => project.Name)];
    }
}
