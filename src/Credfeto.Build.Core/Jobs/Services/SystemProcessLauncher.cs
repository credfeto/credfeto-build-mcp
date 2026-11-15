using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs.Services;

public sealed class SystemProcessLauncher : IProcessLauncher
{
    // A build can leave a child (for example a compiler server) holding the output pipes open after the process
    // itself exits, so output is drained for a bounded time rather than until end of stream
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(10);

    public async ValueTask<int> RunAsync(
        ProcessStep step,
        Func<string, ValueTask> onOutputLine,
        CancellationToken cancellationToken
    )
    {
        using Process process = new() { StartInfo = step.CreateStartInfo() };

        if (!process.Start())
        {
            throw new Win32Exception("The process " + step.FileName + " did not start");
        }

        process.StandardInput.Close();

        Task standardOutput = PumpAsync(reader: process.StandardOutput, onOutputLine: onOutputLine);
        Task standardError = PumpAsync(reader: process.StandardError, onOutputLine: onOutputLine);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await DrainAsync(standardOutput: standardOutput, standardError: standardError);

            throw;
        }

        await DrainAsync(standardOutput: standardOutput, standardError: standardError);

        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, Func<string, ValueTask> onOutputLine)
    {
        try
        {
            for (
                string? line = await reader.ReadLineAsync(CancellationToken.None);
                line is not null;
                line = await reader.ReadLineAsync(CancellationToken.None)
            )
            {
                await onOutputLine(line);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The pipe was closed under the reader once the process was disposed; nothing more can be read
        }
    }

    private static async ValueTask DrainAsync(Task standardOutput, Task standardError)
    {
        try
        {
            // Not the step's token: draining runs after the step was cancelled too, and is bounded by the timeout
            await Task.WhenAll(standardOutput, standardError)
                .WaitAsync(timeout: OutputDrainTimeout, cancellationToken: CancellationToken.None);
        }
        catch (TimeoutException)
        {
            // A detached grandchild still holds the pipe; the process itself has finished
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It already exited
        }
    }
}
