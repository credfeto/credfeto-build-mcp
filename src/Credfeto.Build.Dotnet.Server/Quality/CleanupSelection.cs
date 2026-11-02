using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Dotnet.Server.Quality;

/// <summary>
///     The .cs files a cleanup covers (absolute paths), the base they were compared with, or why none could be
///     chosen.
/// </summary>
[DebuggerDisplay("Files={Files.Count}, BaseReference={BaseReference}, Error={Error}")]
public sealed record CleanupSelection(IReadOnlyList<string> Files, string? BaseReference, string? Error);
