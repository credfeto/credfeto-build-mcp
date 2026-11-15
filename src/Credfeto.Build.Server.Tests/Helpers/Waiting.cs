using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Credfeto.Build.Server.Tests.Helpers;

internal static class Waiting
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    public static async ValueTask ForAsync(Func<bool> condition, string what, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Patience);

        while (!condition())
        {
            try
            {
                await Task.Delay(delay: TimeSpan.FromMilliseconds(50), cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("Timed out waiting for " + what);
            }
        }
    }
}
