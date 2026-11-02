using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

[DebuggerDisplay("Packages={Packages.Count}")]
public sealed record CoberturaReport(IReadOnlyList<CoberturaPackage> Packages);
