using System;
using System.IO;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Dotnet.Server;

public static class SelfTest
{
    private const string SELF_TEST_PROJECT = "SelfTest.Tests";

    public static IServiceCollection Services(BuildServerEnvironment environment)
    {
        return new ServiceCollection().AddDotnetServer(
            environment: environment,
            detection: Detection(environment.RepositoryRoot)
        );
    }

    // Validation on, so a registration that can never resolve fails the self-test rather than the first tool call
    public static IServiceProvider BuildServiceProvider(IServiceCollection services)
    {
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
    }

    // A made-up repository with a unit test project, so every tool is built without reading the real repository
    public static DotnetDetection Detection(string repositoryRoot)
    {
        DotnetProject tests = new(
            Name: SELF_TEST_PROJECT,
            RelativePath: SELF_TEST_PROJECT + "/" + SELF_TEST_PROJECT + ".csproj",
            Kind: DotnetProjectKind.UnitTests
        );
        DotnetRepository repository = new(
            RepositoryRoot: repositoryRoot,
            SolutionPath: Path.Combine(path1: repositoryRoot, path2: "src", path3: "SelfTest.slnx"),
            SolutionDirectory: Path.Combine(path1: repositoryRoot, path2: "src"),
            Projects: [tests],
            UnitTestProjects: [tests],
            Timeout: TimeSpan.FromMinutes(1)
        );

        return new(
            RepositoryRoot: repositoryRoot,
            Repository: repository,
            Reason: "Self-test: the repository is not inspected",
            OverridesError: null
        );
    }
}
