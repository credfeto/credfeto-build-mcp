using System;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Python.Server.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Python.Server;

public static class SelfTest
{
    private const string SELF_TEST_PROJECT = "self-test";

    public static IServiceCollection Services(BuildServerEnvironment environment)
    {
        return new ServiceCollection().AddPythonServer(
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

    // A made-up pip project that declares pytest and coverage, so every tool is built without reading the real
    // repository
    public static PythonDetection Detection(string repositoryRoot)
    {
        PythonProject project = new(
            Name: SELF_TEST_PROJECT,
            RelativeDirectory: PythonProject.ROOT_DIRECTORY,
            Installer: PythonInstaller.Pip
        )
        {
            Manifests = [PyprojectReader.FILE_NAME],
            DeclaresPytest = true,
            DeclaresCoverage = true,
        };
        PythonRepository repository = new(
            RepositoryRoot: repositoryRoot,
            Projects: [project],
            Timeout: TimeSpan.FromMinutes(1)
        );

        return new(
            RepositoryRoot: repositoryRoot,
            Repository: repository,
            Reason: "Self-test: the repository is not inspected",
            OverridesError: null,
            WatchedDirectories: PythonDetector.BaseWatchedDirectories
        );
    }
}
