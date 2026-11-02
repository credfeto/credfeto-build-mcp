using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Repository;

/// <summary>
///     Runs a short read-only git query with a fixed argument vector (never a shell) and returns its output.
/// </summary>
public static class GitCommand
{
    public const string GIT = "git";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Returns git's standard output, or null when git could not run or exited with an error.</summary>
    public static async ValueTask<string?> TryRunAsync(string repositoryRoot, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        string? git = ExecutableSearch.Find(name: GIT, searchPath: Environment.GetEnvironmentVariable("PATH"));

        if (git is null)
        {
            return null;
        }

        using Process process = new() { StartInfo = CreateStartInfo(git: git, repositoryRoot: repositoryRoot, arguments: arguments) };

        try
        {
            if (!process.Start())
            {
                return null;
            }
        }
        catch (Win32Exception)
        {
            // git could not be started
            return null;
        }

        process.StandardInput.Close();

        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            string standardOutput = await output;
            _ = await error;

            return process.ExitCode == 0 ? standardOutput : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);

            return null;
        }
    }

    /// <summary>Splits NUL-separated output (from a <c>-z</c> option) into its non-empty entries.</summary>
    public static IReadOnlyList<string> SplitNul(string? output)
    {
        return output is null ? [] : output.Split(separator: '\0', options: StringSplitOptions.RemoveEmptyEntries);
    }

    private static ProcessStartInfo CreateStartInfo(string git, string repositoryRoot, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = git,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryRoot);

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["LC_ALL"] = "C";

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
