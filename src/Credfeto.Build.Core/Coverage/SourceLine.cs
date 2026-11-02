using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

[DebuggerDisplay("{File}:{Number}")]
public readonly record struct SourceLine(string File, int Number);
