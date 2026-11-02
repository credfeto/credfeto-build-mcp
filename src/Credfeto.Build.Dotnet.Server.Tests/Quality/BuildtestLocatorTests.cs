using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tests.Helpers;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Quality;

public sealed class BuildtestLocatorTests : TestBase, IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        this._directory.Dispose();
    }

    [Fact]
    public async Task TheVariableWinsOverPathAsync()
    {
        string configured = await this.ScriptAsync("configured/buildtest");
        string onPath = await this.ScriptAsync("bin/buildtest");

        BuildtestLocator locator = Locator(new(StringComparer.Ordinal) { [BuildtestLocator.VARIABLE] = configured, ["PATH"] = Path.GetDirectoryName(onPath) });

        Assert.Equal(expected: configured, actual: locator.Require());
    }

    [Fact]
    public async Task OtherwiseItIsFoundOnPathAsync()
    {
        string onPath = await this.ScriptAsync("bin/buildtest");

        BuildtestLocator locator = Locator(new(StringComparer.Ordinal) { ["PATH"] = "/no/such/dir" + Path.PathSeparator + Path.GetDirectoryName(onPath) });

        Assert.Equal(expected: onPath, actual: locator.Require());
    }

    [Theory]
    [InlineData("relative/buildtest")]
    [InlineData("/no/such/buildtest")]
    public void AVariableThatIsNotAnAbsoluteExecutableIsAClearFailure(string configured)
    {
        BuildtestLocator locator = Locator(new(StringComparer.Ordinal) { [BuildtestLocator.VARIABLE] = configured, ["PATH"] = this._directory.Path });

        ToolUnavailableException exception = Assert.Throws<ToolUnavailableException>(locator.Require);
        Assert.Contains(expectedSubstring: BuildtestLocator.VARIABLE, actualString: exception.Message, comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingScriptIsAClearFailure()
    {
        BuildtestLocator locator = Locator(new(StringComparer.Ordinal) { ["PATH"] = this._directory.Path });

        ToolUnavailableException exception = Assert.Throws<ToolUnavailableException>(locator.Require);
        Assert.Contains(expectedSubstring: "buildtest script was not found", actualString: exception.Message, comparisonType: StringComparison.Ordinal);
    }

    private static BuildtestLocator Locator(Dictionary<string, string?> variables)
    {
        return new(name => variables.GetValueOrDefault(name));
    }

    private async ValueTask<string> ScriptAsync(string relative)
    {
        string path = await this._directory.WriteAsync(relative: relative, content: "#!/bin/sh\nexit 0\n", cancellationToken: this.CancellationToken());
        Executable.Mark(path);

        return path;
    }
}
