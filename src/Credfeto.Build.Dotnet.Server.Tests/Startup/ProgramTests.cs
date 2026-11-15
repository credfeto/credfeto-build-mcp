using System.Threading.Tasks;
using Credfeto.Build.Server;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Startup;

public sealed class ProgramTests : TestBase
{
    [Fact]
    public async Task HelpExitsWithoutServingAsync()
    {
        int exitCode = await Program.Main([ServerCommandLine.HELP]);

        Assert.Equal(expected: ServerCommandLine.SUCCESS, actual: exitCode);
    }

    [Fact]
    public async Task AnUnknownSwitchExitsWithAUsageErrorAsync()
    {
        int exitCode = await Program.Main(["--unknown"]);

        Assert.Equal(expected: ServerCommandLine.USAGE_ERROR, actual: exitCode);
    }
}
