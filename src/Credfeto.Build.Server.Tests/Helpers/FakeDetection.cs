using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Server.Tests.Helpers;

[DebuggerDisplay("Signature={Signature}, Tools={ToolNames.Count}")]
internal sealed record FakeDetection(string Signature, IReadOnlyList<string> ToolNames);
