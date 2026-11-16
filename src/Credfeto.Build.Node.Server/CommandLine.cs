using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Server;

namespace Credfeto.Build.Node.Server;

public static class CommandLine
{
    private static readonly ServerCommandLine Server = new(
        serverName: ServerOptionsFactory.SERVER_NAME,
        selfTestServices: SelfTest.Services,
        buildServiceProvider: SelfTest.BuildServiceProvider
    );

    public static string Usage => Server.Usage;

    public static ValueTask<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextWriter output,
        TextWriter error,
        Func<CancellationToken, ValueTask<BuildServerEnvironment>> resolveEnvironment,
        Func<BuildServerEnvironment, CancellationToken, ValueTask<int>> serve,
        CancellationToken cancellationToken
    )
    {
        return Server.RunAsync(
            arguments: arguments,
            output: output,
            error: error,
            resolveEnvironment: resolveEnvironment,
            serve: serve,
            cancellationToken: cancellationToken
        );
    }
}
