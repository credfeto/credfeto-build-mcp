using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Startup;

public sealed class ProgramTests : TestBase
{
    [Fact]
    public async Task HelpExitsWithoutServingAsync()
    {
        int exitCode = await Program.Main([CommandLine.HELP]);

        Assert.Equal(expected: CommandLine.SUCCESS, actual: exitCode);
    }

    [Fact]
    public async Task AnUnknownSwitchExitsWithAUsageErrorAsync()
    {
        int exitCode = await Program.Main(["--unknown"]);

        Assert.Equal(expected: CommandLine.USAGE_ERROR, actual: exitCode);
    }
}
