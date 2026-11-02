using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Core.Coverage;

/// <summary>One package (assembly) of a Cobertura report: every instrumented line and whether any test hit it.</summary>
[DebuggerDisplay("Name={Name}, Lines={Lines.Count}")]
public sealed record CoberturaPackage(string Name, IReadOnlyDictionary<SourceLine, bool> Lines);
