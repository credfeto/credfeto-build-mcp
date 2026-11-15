using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Services;
using Credfeto.Build.Server.Commands;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Server.Tests.Commands;

public sealed class ProcessCommandRunnerTests : TestBase, IDisposable
{
    private const string SECRET = "s3cr3t-value-for-masking";

    private readonly TemporaryDirectory _directory = new();
    private readonly ProcessCommandRunner _runner = new(
        new OutputMasker(new Dictionary<string, string>(StringComparer.Ordinal) { ["EXAMPLE_TOKEN"] = SECRET })
    );

    public void Dispose()
    {
        this._directory.Dispose();
    }

    [Fact]
    public async Task ReturnsTheExitCodeAndTheMaskedStandardOutputAsync()
    {
        ProcessStep step = await this.ScriptAsync("echo \"out $1 " + SECRET + "\"\necho err >&2\nexit 3\n");

        CommandOutput output = await this._runner.RunAsync(
            step: step,
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 3, actual: output.ExitCode);
        Assert.False(condition: output.TimedOut, userMessage: "it finished");
        Assert.False(condition: output.Succeeded, userMessage: "exit 3");
        Assert.Equal(expected: "out arg " + OutputMasker.REPLACEMENT + "\n", actual: output.StandardOutput);
    }

    [Fact]
    public async Task ACommandPastItsTimeoutIsKilledAsync()
    {
        ProcessStep step = await this.ScriptAsync("sleep 30\n");

        CommandOutput output = await this._runner.RunAsync(
            step: step,
            timeout: TimeSpan.FromMilliseconds(200),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: output.TimedOut, userMessage: "the timeout applies");
        Assert.False(condition: output.Succeeded, userMessage: "timed out");
    }

    [Fact]
    public async Task ACancelledCallKillsTheCommandAsync()
    {
        string pidFile = this._directory.Combine("pid");
        ProcessStep step = await this.ScriptAsync("echo $$ > \"" + pidFile + "\"\nexec sleep 30\n");
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            _ = await this._runner.RunAsync(
                step: step,
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellation.Token
            )
        );

        string pid = (await File.ReadAllTextAsync(path: pidFile, cancellationToken: this.CancellationToken())).Trim();
        Assert.True(condition: await this.HasExitedAsync(pid), userMessage: "the cancelled process was killed");
    }

    private async ValueTask<bool> HasExitedAsync(string pid)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            // A killed child can linger briefly as a zombie until it is reaped
            if (
                !File.Exists("/proc/" + pid + "/cmdline")
                || (
                    await File.ReadAllTextAsync(
                        path: "/proc/" + pid + "/cmdline",
                        cancellationToken: this.CancellationToken()
                    )
                ).Length == 0
            )
            {
                return true;
            }

            await Task.Delay(millisecondsDelay: 100, cancellationToken: this.CancellationToken());
        }

        return false;
    }

    [Fact]
    public async Task AMissingExecutableDoesNotStartAsync()
    {
        ProcessStep step = new(
            Name: "missing",
            FileName: this._directory.Combine("does-not-exist"),
            Arguments: [],
            WorkingDirectory: this._directory.Path,
            Environment: new Dictionary<string, string>(StringComparer.Ordinal)
        );

        CommandOutput output = await this._runner.RunAsync(
            step: step,
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: CommandOutput.NOT_STARTED, actual: output.ExitCode);
        Assert.False(condition: output.TimedOut, userMessage: "it never started");
    }

    private async ValueTask<ProcessStep> ScriptAsync(string body)
    {
        string script = await this._directory.WriteAsync(
            relative: "script.sh",
            content: "#!/bin/sh\n" + body,
            cancellationToken: this.CancellationToken()
        );
        Executable.Mark(script);

        return new(
            Name: "script",
            FileName: script,
            Arguments: ["arg"],
            WorkingDirectory: this._directory.Path,
            Environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["EXTRA"] = "1" }
        );
    }
}
