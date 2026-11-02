using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class ExecutableSearchTests : TestBase
{
    [Fact]
    public async Task FindsTheFirstExecutableOnTheSearchPathAsync()
    {
        using TemporaryDirectory directory = new();
        string notExecutable = await directory.WriteAsync(relative: "first/tool", content: "#!/bin/sh\n", cancellationToken: this.CancellationToken());
        string executable = await directory.WriteAsync(relative: "second/tool", content: "#!/bin/sh\n", cancellationToken: this.CancellationToken());
        Assert.SkipWhen(condition: OperatingSystem.IsWindows(), reason: "Unix file modes");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path: notExecutable, mode: UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(path: executable, mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        string searchPath = string.Join(Path.PathSeparator, "relative/dir", directory.Combine("first"), directory.Combine("second"));

        Assert.Equal(expected: executable, actual: ExecutableSearch.Find(name: "tool", searchPath: searchPath));
        Assert.Null(ExecutableSearch.Find(name: "missing", searchPath: searchPath));
        Assert.Null(ExecutableSearch.Find(name: "tool", searchPath: null));
    }

    [Fact]
    public async Task AnUnreadableLinkIsSkippedAsync()
    {
        Assert.SkipWhen(condition: OperatingSystem.IsWindows(), reason: "Unix file modes");

        using TemporaryDirectory directory = new();
        string target = await directory.WriteAsync(relative: "private/tool", content: "#!/bin/sh\n", cancellationToken: this.CancellationToken());
        string executable = await directory.WriteAsync(relative: "second/tool", content: "#!/bin/sh\n", cancellationToken: this.CancellationToken());
        _ = Directory.CreateDirectory(directory.Combine("first"));
        _ = File.CreateSymbolicLink(path: directory.Combine("first/tool"), pathToTarget: target);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path: executable, mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(path: directory.Combine("private"), mode: UnixFileMode.None);
        }

        try
        {
            string searchPath = string.Join(Path.PathSeparator, directory.Combine("first"), directory.Combine("second"));

            Assert.Equal(expected: executable, actual: ExecutableSearch.Find(name: "tool", searchPath: searchPath));
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path: directory.Combine("private"), mode: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }
}
