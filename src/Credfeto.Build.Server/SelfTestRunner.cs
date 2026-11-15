using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Server;

public static class SelfTestRunner
{
    public const int PASSED = 0;
    public const int FAILED = 1;
    public const string FAILURE_PREFIX = "self-test failed: ";

    private static readonly char[] LineBreaks = ['\r', '\n'];

    // Resolves every registration without starting the transport or the repository watcher. The provider comes from
    // the server, which builds it with build-time and scope validation on
    public static async ValueTask<int> RunAsync(
        IServiceCollection services,
        Func<IServiceCollection, IServiceProvider> buildServiceProvider,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await ResolveAllAsync(services: services, buildServiceProvider: buildServiceProvider);

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

    private static async ValueTask ResolveAllAsync(
        IServiceCollection services,
        Func<IServiceCollection, IServiceProvider> buildServiceProvider
    )
    {
        IServiceProvider provider = buildServiceProvider(services);

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
            await DisposeAsync(provider);
        }
    }

    private static ValueTask DisposeAsync(IServiceProvider provider)
    {
        switch (provider)
        {
            case IAsyncDisposable asyncDisposable:
                return asyncDisposable.DisposeAsync();
            case IDisposable disposable:
                disposable.Dispose();

                return ValueTask.CompletedTask;
            default:
                return ValueTask.CompletedTask;
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
