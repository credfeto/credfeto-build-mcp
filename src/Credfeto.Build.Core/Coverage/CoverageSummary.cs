using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

[DebuggerDisplay("Assemblies={Assemblies.Count}, Overall={Overall.LineCoverage}")]
public sealed record CoverageSummary(IReadOnlyList<AssemblyCoverage> Assemblies, AssemblyCoverage Overall);
