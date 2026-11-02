using System;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Core.Overrides;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Detection;

public sealed class DotnetDetectorTests : TestBase
{
    [Fact]
    public async Task SolutionUnderSrcIsFoundAndProjectsAreClassifiedAsync()
    {
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());

        DotnetRepository detected = await this.DetectRequiredAsync(repository);

        Assert.Equal(expected: repository.Combine("src/Example.slnx"), actual: detected.SolutionPath);
        Assert.Equal(expected: repository.Combine("src"), actual: detected.SolutionDirectory);
        Assert.Equal(
            expected: ["Example.Api.Tests", "Example.Core.Tests"],
            actual: detected.UnitTestProjects.Select(static project => project.Name),
            comparer: StringComparer.Ordinal
        );
        Assert.Equal(expected: "Example.Core.Tests/Example.Core.Tests.csproj", actual: detected.UnitTestProjects[1].RelativePath);
        Assert.Equal(expected: DotnetProjectKind.IntegrationTests, actual: Kind(detected: detected, name: "Example.Integration.Tests"));
        Assert.Equal(expected: DotnetProjectKind.BenchmarkTests, actual: Kind(detected: detected, name: "Example.Benchmark.Tests"));
        Assert.Equal(expected: DotnetProjectKind.Project, actual: Kind(detected: detected, name: "Example.Tests.Support"));
        Assert.Equal(expected: DotnetProjectKind.Project, actual: Kind(detected: detected, name: "Example.Mocks.Tests"));
        Assert.DoesNotContain(collection: detected.Projects, filter: static project => StringComparer.Ordinal.Equals(x: project.Name, y: "Example.Missing.Tests"));
        Assert.Equal(expected: TimeSpan.FromMinutes(30), actual: detected.Timeout);
    }

    [Fact]
    public async Task SlnxIsPreferredToSlnAndSrcToTheRootAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(relative: "Root.slnx", content: "<Solution />", cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(relative: "src/B.sln", content: string.Empty, cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(relative: "src/A.slnx", content: "<Solution />", cancellationToken: this.CancellationToken());

        DotnetRepository detected = await this.DetectRequiredAsync(repository);

        Assert.Equal(expected: repository.Combine("src/A.slnx"), actual: detected.SolutionPath);
    }

    [Fact]
    public async Task SlnAtTheRootIsReadAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(
            relative: "Example.sln",
            content: """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Example.Core.Tests", "tests\Example.Core.Tests\Example.Core.Tests.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Solution Items", "Solution Items", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                """,
            cancellationToken: this.CancellationToken()
        );
        _ = await repository.WriteAsync(relative: "tests/Example.Core.Tests/Example.Core.Tests.csproj", content: "<Project />", cancellationToken: this.CancellationToken());

        DotnetRepository detected = await this.DetectRequiredAsync(repository);

        DotnetProject project = Assert.Single(detected.UnitTestProjects);
        Assert.Equal(expected: "tests/Example.Core.Tests/Example.Core.Tests.csproj", actual: project.RelativePath);
        Assert.Equal(expected: repository.Path, actual: detected.SolutionDirectory);
    }

    [Fact]
    public async Task RepositoryWithoutASolutionIsNotDotnetAsync()
    {
        using TemporaryDirectory repository = new();
        _ = await repository.WriteAsync(relative: "package.json", content: "{}", cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(relative: "deep/down/src/Lost.slnx", content: "<Solution />", cancellationToken: this.CancellationToken());

        DotnetDetection detection = await this.DetectAsync(repository.Path);

        Assert.Null(detection.Repository);
        Assert.Contains(expectedSubstring: "No .slnx or .sln", actualString: detection.Reason, comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingRootIsNotDotnetAsync()
    {
        using TemporaryDirectory repository = new();

        DotnetDetection detection = await this.DetectAsync(repository.Combine("absent"));

        Assert.Null(detection.Repository);
    }

    [Fact]
    public async Task OverridesCanDisableDotnetAsync()
    {
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(relative: OverridesLoader.RELATIVE_PATH, content: """{"dotnet": {"enabled": false}}""", cancellationToken: this.CancellationToken());

        DotnetDetection detection = await this.DetectAsync(repository.Path);

        Assert.Null(detection.Repository);
        Assert.Contains(expectedSubstring: "Disabled", actualString: detection.Reason, comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverridesNarrowTestsSetTheTimeoutAndChooseTheSolutionAsync()
    {
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(relative: "src/Other.slnx", content: SampleRepository.SLNX, cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(
            relative: OverridesLoader.RELATIVE_PATH,
            content: """{"dotnet": {"solution": "src/Other.slnx", "excludeTestProjects": ["Example.Api.Tests"], "timeoutMinutes": 999}}""",
            cancellationToken: this.CancellationToken()
        );

        DotnetDetection detection = await this.DetectAsync(repository.Path);

        Assert.Null(detection.OverridesError);
        DotnetRepository? detected = detection.Repository;
        Assert.NotNull(detected);
        Assert.Equal(expected: repository.Combine("src/Other.slnx"), actual: detected.SolutionPath);
        Assert.Equal(expected: "Example.Core.Tests", actual: Assert.Single(detected.UnitTestProjects).Name);
        Assert.Equal(expected: TimeSpan.FromMinutes(240), actual: detected.Timeout);
    }

    [Theory]
    [InlineData("../outside.slnx")]
    [InlineData("src/Example.Core/Example.Core.csproj")]
    [InlineData("src/Absent.slnx")]
    public async Task OverrideSolutionOutsideTheRepositoryOrNotASolutionIsIgnoredAsync(string solution)
    {
        using TemporaryDirectory repository = new();
        await SampleRepository.WriteAsync(repository: repository, cancellationToken: this.CancellationToken());
        _ = await repository.WriteAsync(relative: OverridesLoader.RELATIVE_PATH, content: "{\"dotnet\": {\"solution\": \"" + solution + "\"}}", cancellationToken: this.CancellationToken());

        DotnetDetection detection = await this.DetectAsync(repository.Path);

        Assert.NotNull(detection.OverridesError);
        Assert.Equal(expected: repository.Combine("src/Example.slnx"), actual: detection.Repository?.SolutionPath);
    }

    [Theory]
    [InlineData("Example.Tests", DotnetProjectKind.UnitTests)]
    [InlineData("Example.Integration.Tests", DotnetProjectKind.IntegrationTests)]
    [InlineData("Example.Benchmark.Tests", DotnetProjectKind.BenchmarkTests)]
    [InlineData("Example.TestHarness", DotnetProjectKind.Project)]
    [InlineData("Example.Tests.Common", DotnetProjectKind.Project)]
    [InlineData("Example.tests", DotnetProjectKind.Project)]
    public static void NamingConventionDecidesTheKind(string name, DotnetProjectKind expected)
    {
        Assert.Equal(expected: expected, actual: DotnetDetector.KindOf(name));
    }

    private static DotnetProjectKind Kind(DotnetRepository detected, string name)
    {
        return detected.Projects.Single(project => StringComparer.Ordinal.Equals(x: project.Name, y: name)).Kind;
    }

    private async ValueTask<DotnetRepository> DetectRequiredAsync(TemporaryDirectory repository)
    {
        DotnetDetection detection = await this.DetectAsync(repository.Path);

        return detection.Repository ?? throw new InvalidOperationException("Not detected: " + detection.Reason);
    }

    private ValueTask<DotnetDetection> DetectAsync(string root)
    {
        return DotnetDetector.DetectAsync(repositoryRoot: root, cancellationToken: this.CancellationToken());
    }
}
