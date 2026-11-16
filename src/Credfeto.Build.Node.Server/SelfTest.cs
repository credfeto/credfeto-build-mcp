using System;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Node.Server.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Node.Server;

public static class SelfTest
{
    private const string SELF_TEST_PACKAGE = "self-test";

    public static IServiceCollection Services(BuildServerEnvironment environment)
    {
        return new ServiceCollection().AddNodeServer(
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

    // A made-up npm repository with one package, so every tool is built without reading the real repository
    public static NodeDetection Detection(string repositoryRoot)
    {
        NodePackage package = new(
            Name: SELF_TEST_PACKAGE,
            RelativeDirectory: NodePackage.ROOT_DIRECTORY,
            Scripts: [NodeScripts.TEST]
        );
        NodeRepository repository = new(
            RepositoryRoot: repositoryRoot,
            InstallRoots:
            [
                new NodeInstallRoot(
                    RelativeDirectory: NodePackage.ROOT_DIRECTORY,
                    LockFile: NodeDetector.NPM_LOCK_FILE
                ),
            ],
            Packages: [package],
            Timeout: TimeSpan.FromMinutes(1)
        );

        return new(
            RepositoryRoot: repositoryRoot,
            Repository: repository,
            Reason: "Self-test: the repository is not inspected",
            OverridesError: null,
            WatchedDirectories: NodeDetector.BaseWatchedDirectories
        );
    }
}
