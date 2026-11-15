using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;

namespace Credfeto.Build.Testing;

// Starts a server binary for one repository and times process start to the tools/list reply
public static class McpServerProcess
{
    public static async ValueTask<TimedProbeResult> StartAndListAsync(
        string binary,
        string repositoryRoot,
        string logDirectory,
        CancellationToken cancellationToken
    )
    {
        ProcessStartInfo startInfo = new(binary)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment[BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE] = repositoryRoot;
        startInfo.Environment[BuildServerEnvironment.LOG_DIRECTORY_VARIABLE] = logDirectory;

        long started = Stopwatch.GetTimestamp();
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The server did not start");

        try
        {
            McpProbeResult result = await McpProbe.ListToolsAsync(
                toServer: process.StandardInput.BaseStream,
                fromServer: process.StandardOutput,
                cancellationToken: cancellationToken
            );
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);

            return new(Result: result, Elapsed: elapsed);
        }
        finally
        {
            // A probe that failed or was cancelled must not leave the server running
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
