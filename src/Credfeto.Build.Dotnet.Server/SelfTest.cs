using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Dotnet.Server.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Dotnet.Server;

public static class SelfTest
{
    public const int PASSED = 0;
    public const int FAILED = 1;
    public const string FAILURE_PREFIX = "self-test failed: ";

    private const string SELF_TEST_PROJECT = "SelfTest.Tests";

    private static readonly char[] LineBreaks = ['\r', '\n'];

    public static IServiceCollection Services(BuildServerEnvironment environment)
    {
        return new ServiceCollection().AddDotnetServer(
            environment: environment,
            detection: Detection(environment.RepositoryRoot)
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

    // Resolves every registration without starting the transport or the repository watcher
    public static async ValueTask<int> RunAsync(
        IServiceCollection services,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await ResolveAllAsync(services);

            return PASSED;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(
                buffer: (FAILURE_PREFIX + OneLine(exception.Message)).AsMemory(),
                cancellationToken: cancellationToken
            );

            return FAILED;
        }
    }

    private static async ValueTask ResolveAllAsync(IServiceCollection services)
    {
        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        try
        {
            foreach (
                Type serviceType in services
                    .Select(static descriptor => descriptor.ServiceType)
                    .Where(static type => !type.IsGenericTypeDefinition)
            )
            {
                _ = provider.GetRequiredService(serviceType);
            }
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    private static string OneLine(string message)
    {
        return string.Join(
            separator: ' ',
            value: message.Split(
                separator: LineBreaks,
                options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        );
    }
}
