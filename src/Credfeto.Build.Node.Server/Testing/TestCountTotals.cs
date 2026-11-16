using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Node.Server.Testing;

// Sums the summary counts of every test run in a job; null until a run reported a summary
internal sealed class TestCountTotals
{
    private int _failed;
    private int _passed;
    private bool _seen;
    private int _skipped;
    private int _total;

    public TestCounts? Counts =>
        this._seen ? new(Total: this._total, Passed: this._passed, Failed: this._failed, Skipped: this._skipped) : null;

    public void Add(int total, int passed, int failed, int skipped)
    {
        this._seen = true;
        this._total += total;
        this._passed += passed;
        this._failed += failed;
        this._skipped += skipped;
    }
}
