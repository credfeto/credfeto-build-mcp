using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Dotnet.Server.Commands;

// Starts the step's executable directly (no shell) with stdin closed; standard output is masked, standard error is
// read and dropped so the process never blocks on a full pipe
public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly char[] LineBreaks = ['\n'];

    private readonly IOutputMasker _masker;

    public ProcessCommandRunner(IOutputMasker masker)
    {
        this._masker = masker;
    }

    public async ValueTask<CommandOutput> RunAsync(
        ProcessStep step,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        using Process process = new() { StartInfo = CreateStartInfo(step) };

        try
        {
            if (!process.Start())
            {
                return NotStarted();
            }
        }
        catch (Win32Exception)
        {
            // The executable could not be started
            return NotStarted();
        }

        process.StandardInput.Close();

        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            string standardOutput = await output;
            _ = await error;

            return new(ExitCode: process.ExitCode, StandardOutput: this.Mask(standardOutput), TimedOut: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);

            return new(ExitCode: CommandOutput.NOT_STARTED, StandardOutput: string.Empty, TimedOut: true);
        }
    }

    private static CommandOutput NotStarted()
    {
        return new(ExitCode: CommandOutput.NOT_STARTED, StandardOutput: string.Empty, TimedOut: false);
    }

    private string Mask(string output)
    {
        return string.Join(separator: '\n', values: output.Split(LineBreaks).Select(this._masker.Mask));
    }

    private static ProcessStartInfo CreateStartInfo(ProcessStep step)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = step.FileName,
            WorkingDirectory = step.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in step.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string> variable in step.Environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
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
