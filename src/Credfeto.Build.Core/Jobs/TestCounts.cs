using System.Diagnostics;

namespace Credfeto.Build.Core.Jobs;

[DebuggerDisplay("Total={Total}, Passed={Passed}, Failed={Failed}, Skipped={Skipped}")]
public sealed record TestCounts(int Total, int Passed, int Failed, int Skipped);
