using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Json;
using Credfeto.Build.Dotnet.Server.Models;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class DetectTool : BuildTool
{
    private readonly ToolContext _context;

    public DetectTool(ToolContext context)
        : base(
            name: ToolNames.DETECT,
            title: "Detect .NET",
            description: "Lists the solution, its projects and the unit test projects the test and coverage tools can run (*.Tests, excluding *.Integration.Tests and *.Benchmark.Tests), as they are now (the repository is detected afresh on every call)",
            schema: [],
            readOnly: true
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        DotnetDetection detection = await this._context.Detection.RefreshAsync(cancellationToken);
        DotnetRepository repository = detection.Repository
            ?? throw new ToolUnavailableException("There is no .NET solution in the repository any more (" + detection.Reason + ")");

        DetectResponse response = new(
            RepositoryRoot: repository.RepositoryRoot,
            Solution: RepositoryPaths.RelativeTo(root: repository.RepositoryRoot, path: repository.SolutionPath),
            SolutionDirectory: RepositoryPaths.RelativeTo(root: repository.RepositoryRoot, path: repository.SolutionDirectory),
            Projects: [.. repository.Projects.Select(static project => project.RelativePath)],
            UnitTestProjects: [.. repository.UnitTestProjects.Select(static project => project.Name)],
            IntegrationTestProjects: NamesOf(repository: repository, kind: DotnetProjectKind.IntegrationTests),
            BenchmarkTestProjects: NamesOf(repository: repository, kind: DotnetProjectKind.BenchmarkTests),
            TimeoutMinutes: (int)repository.Timeout.TotalMinutes,
            OverridesError: detection.OverridesError
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: DotnetJsonContext.Default.DetectResponse);
    }

    private static string[] NamesOf(DotnetRepository repository, DotnetProjectKind kind)
    {
        return [.. repository.Projects.Where(project => project.Kind == kind).Select(static project => project.Name)];
    }
}
