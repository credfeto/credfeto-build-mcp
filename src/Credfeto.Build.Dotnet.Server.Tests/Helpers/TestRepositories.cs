using System;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

internal static class TestRepositories
{
    public static readonly DotnetProject CoreTests = new(
        Name: "Example.Core.Tests",
        RelativePath: "Example.Core.Tests/Example.Core.Tests.csproj",
        Kind: DotnetProjectKind.UnitTests
    );

    public static readonly DotnetProject ApiTests = new(
        Name: "Example.Api.Tests",
        RelativePath: "Example.Api.Tests/Example.Api.Tests.csproj",
        Kind: DotnetProjectKind.UnitTests
    );

    public static readonly DotnetProject Core = new(
        Name: "Example.Core",
        RelativePath: "Example.Core/Example.Core.csproj",
        Kind: DotnetProjectKind.Project
    );

    public static readonly DotnetProject IntegrationTests = new(
        Name: "Example.Integration.Tests",
        RelativePath: "Example.Integration.Tests/Example.Integration.Tests.csproj",
        Kind: DotnetProjectKind.IntegrationTests
    );

    public static readonly DotnetProject BenchmarkTests = new(
        Name: "Example.Benchmark.Tests",
        RelativePath: "Example.Benchmark.Tests/Example.Benchmark.Tests.csproj",
        Kind: DotnetProjectKind.BenchmarkTests
    );

    public static DotnetRepository Create(string root, params DotnetProject[] unitTests)
    {
        return new(
            RepositoryRoot: root,
            SolutionPath: root + "/src/Example.slnx",
            SolutionDirectory: root + "/src",
            Projects: [Core, IntegrationTests, BenchmarkTests, .. unitTests],
            UnitTestProjects: unitTests,
            Timeout: TimeSpan.FromMinutes(30)
        );
    }

    public static DotnetDetection Detected(DotnetRepository repository)
    {
        return new(
            RepositoryRoot: repository.RepositoryRoot,
            Repository: repository,
            Reason: "Found src/Example.slnx",
            OverridesError: null
        );
    }

    public static DotnetDetection NotDetected(string root)
    {
        return new(
            RepositoryRoot: root,
            Repository: null,
            Reason: "No .slnx or .sln under src/ or the repository root",
            OverridesError: null
        );
    }
}
