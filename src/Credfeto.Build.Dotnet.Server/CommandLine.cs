using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Configuration.Exceptions;

namespace Credfeto.Build.Dotnet.Server;

public static class CommandLine
{
    public const string HELP = "--help";
    public const string SHORT_HELP = "-h";
    public const string SELF_TEST = "--self-test";
    public const int SUCCESS = 0;
    public const int CONFIGURATION_ERROR = 1;
    public const int USAGE_ERROR = 2;

    public static readonly string Usage = string.Join(
        separator: Environment.NewLine,
        "Usage: " + ServerOptionsFactory.SERVER_NAME + " [" + SELF_TEST + " | " + HELP + "]",
        "",
        "With no option, serves MCP over stdio for the repository in "
            + BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE
            + " (when unset, "
            + BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT
            + " if it exists, else the git root of the current directory; a value that is not an absolute path is an",
        "error). The chosen repository root and where it came from are written to stderr at startup.",
        "",
        "Options:",
        "  "
            + SELF_TEST
            + "  Builds the service provider and resolves every dependency, then exits: 0 when that works, 1 with",
        "               a one-line reason on stderr when it does not. Starts no MCP transport, reads no input and opens",
        "               no repository, file or network connection; only git may run, to find the repository root.",
        "  " + SHORT_HELP + ", " + HELP + "   Shows this help and exits."
    );

    public static async ValueTask<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextWriter output,
        TextWriter error,
        Func<CancellationToken, ValueTask<BuildServerEnvironment>> resolveEnvironment,
        Func<BuildServerEnvironment, CancellationToken, ValueTask<int>> serve,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await DispatchAsync(
                arguments: arguments,
                output: output,
                error: error,
                resolveEnvironment: resolveEnvironment,
                serve: serve,
                cancellationToken: cancellationToken
            );
        }
        catch (InvalidBuildConfigurationException exception)
        {
            // stderr only: stdout is the MCP stream
            await error.WriteLineAsync(buffer: exception.Message.AsMemory(), cancellationToken: cancellationToken);

            return CONFIGURATION_ERROR;
        }
    }

    private static async ValueTask<int> DispatchAsync(
        IReadOnlyList<string> arguments,
        TextWriter output,
        TextWriter error,
        Func<CancellationToken, ValueTask<BuildServerEnvironment>> resolveEnvironment,
        Func<BuildServerEnvironment, CancellationToken, ValueTask<int>> serve,
        CancellationToken cancellationToken
    )
    {
        switch (arguments)
        {
            case []:
                return await ServeAsync(
                    error: error,
                    resolveEnvironment: resolveEnvironment,
                    serve: serve,
                    cancellationToken: cancellationToken
                );

            case [HELP]
            or [SHORT_HELP]:
                await output.WriteLineAsync(buffer: Usage.AsMemory(), cancellationToken: cancellationToken);

                return SUCCESS;

            case [SELF_TEST]:
                return await SelfTest.RunAsync(
                    services: SelfTest.Services(await resolveEnvironment(cancellationToken)),
                    error: error,
                    cancellationToken: cancellationToken
                );

            default:
                await error.WriteLineAsync(
                    buffer: (
                        "Unrecognised arguments: "
                        + string.Join(separator: ' ', values: arguments)
                        + " (see "
                        + HELP
                        + ")"
                    ).AsMemory(),
                    cancellationToken: cancellationToken
                );

                return USAGE_ERROR;
        }
    }

    private static async ValueTask<int> ServeAsync(
        TextWriter error,
        Func<CancellationToken, ValueTask<BuildServerEnvironment>> resolveEnvironment,
        Func<BuildServerEnvironment, CancellationToken, ValueTask<int>> serve,
        CancellationToken cancellationToken
    )
    {
        BuildServerEnvironment environment = await resolveEnvironment(cancellationToken);

        // stderr only: stdout is the MCP stream
        await error.WriteLineAsync(
            buffer: (ServerOptionsFactory.SERVER_NAME + ": " + environment.DescribeRepositoryRoot()).AsMemory(),
            cancellationToken: cancellationToken
        );

        return await serve(environment, cancellationToken);
    }
}
