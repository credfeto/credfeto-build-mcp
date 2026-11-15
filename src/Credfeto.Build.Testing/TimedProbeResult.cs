using System;
using System.Diagnostics;

namespace Credfeto.Build.Testing;

[DebuggerDisplay("Elapsed={Elapsed}")]
public sealed record TimedProbeResult(McpProbeResult Result, TimeSpan Elapsed);
