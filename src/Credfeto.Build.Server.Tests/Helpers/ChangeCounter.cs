using System.Threading;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Tests.Helpers;

internal sealed class ChangeCounter
{
    private int _count;

    private ChangeCounter() { }

    public int Count => Volatile.Read(ref this._count);

    public static ChangeCounter Watch(McpServerPrimitiveCollection<McpServerTool> tools)
    {
        ChangeCounter counter = new();
        tools.Changed += (_, _) => Interlocked.Increment(ref counter._count);

        return counter;
    }
}
