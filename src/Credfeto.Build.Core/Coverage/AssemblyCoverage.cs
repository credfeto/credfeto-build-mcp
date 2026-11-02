using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

[DebuggerDisplay("Name={Name}, LinesCovered={LinesCovered}, LinesValid={LinesValid}, LineCoverage={LineCoverage}")]
public sealed record AssemblyCoverage(string Name, int LinesCovered, int LinesValid, double LineCoverage);
