using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Jobs.Services;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Commands;

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
