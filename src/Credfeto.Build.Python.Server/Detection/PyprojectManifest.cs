using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Detection;

// What the server reads from a pyproject.toml. HasProjectTable is whether it has a [project] table at all: one with
// only [tool.*] settings is not a project. The extras and groups are keyed by their names, groups with their
// include-group entries expanded
[DebuggerDisplay("HasProjectTable={HasProjectTable}, Name={Name}")]
public sealed record PyprojectManifest(bool HasProjectTable, string? Name, string? RequiresPython)
{
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> OptionalDependencies { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DependencyGroups { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.Ordinal);

    // [tool.poetry]: poetry's own project metadata, which pip and uv cannot install from
    public bool IsPoetry { get; init; }
}
