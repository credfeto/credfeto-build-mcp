using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Server.Tests.Helpers;

internal sealed class FakeTool : BuildTool
{
    public FakeTool(string name)
        : base(name: name, title: "Fake " + name, description: "A tool that does nothing", schema: [], readOnly: true)
    { }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult("{}");
    }
}
