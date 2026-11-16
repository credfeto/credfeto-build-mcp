using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Testing;

public sealed class CompositeTestOutputParser : ITestOutputParser
{
    private readonly IReadOnlyList<ITestOutputParser> _parsers;

    public CompositeTestOutputParser(IReadOnlyList<ITestOutputParser> parsers)
    {
        this._parsers = parsers;
    }

    public TestCounts? Counts
    {
        get
        {
            TestCounts[] counts = [.. this._parsers.Select(static parser => parser.Counts).OfType<TestCounts>()];

            return counts.Length == 0
                ? null
                : new(
                    Total: counts.Sum(static count => count.Total),
                    Passed: counts.Sum(static count => count.Passed),
                    Failed: counts.Sum(static count => count.Failed),
                    Skipped: counts.Sum(static count => count.Skipped)
                );
        }
    }

    public IReadOnlyList<string> FailedTests => [.. this._parsers.SelectMany(static parser => parser.FailedTests)];

    public static CompositeTestOutputParser For(IEnumerable<NodeTestRunner> runners)
    {
        return new([.. runners.Distinct().Order().Select(static runner => Create(runner)).OfType<ITestOutputParser>()]);
    }

    public void Observe(string line)
    {
        foreach (ITestOutputParser parser in this._parsers)
        {
            parser.Observe(line);
        }
    }

    private static ITestOutputParser? Create(NodeTestRunner runner)
    {
        return runner switch
        {
            NodeTestRunner.Vitest => new VitestOutputParser(),
            NodeTestRunner.Jest => new JestOutputParser(),
            NodeTestRunner.NodeTest => new NodeTestOutputParser(),
            _ => null,
        };
    }
}
