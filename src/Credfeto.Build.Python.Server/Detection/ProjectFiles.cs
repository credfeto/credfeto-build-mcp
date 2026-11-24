using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Credfeto.Build.Python.Server.Detection;

// The manifest and lock files in one directory
[DebuggerDisplay("HasPyproject={HasPyproject}, RequirementFiles={RequirementFiles.Count}, HasUvLock={HasUvLock}")]
internal sealed record ProjectFiles(
    bool HasPyproject,
    IReadOnlyList<string> RequirementFiles,
    bool HasUvLock,
    UnsupportedManifest? UnsupportedManifest
)
{
    public IReadOnlyList<string> Manifests =>
        [
            .. this.HasPyproject ? [PyprojectReader.FILE_NAME] : (string[])[],
            .. this.RequirementFiles,
            .. this.HasUvLock ? [ManifestFiles.UV_LOCK] : (string[])[],
            .. this.UnsupportedManifest is { } unsupported ? [unsupported.FileName] : (string[])[],
        ];

    public static ProjectFiles Find(string directory)
    {
        return new(
            HasPyproject: File.Exists(Path.Combine(path1: directory, path2: PyprojectReader.FILE_NAME)),
            RequirementFiles: RequirementsReader.Find(directory),
            HasUvLock: File.Exists(Path.Combine(path1: directory, path2: ManifestFiles.UV_LOCK)),
            UnsupportedManifest: ManifestFiles.Unsupported.FirstOrDefault(manifest =>
                File.Exists(Path.Combine(path1: directory, path2: manifest.FileName))
            )
        );
    }

    // A project has a [project] table or a requirements file; an unreadable pyproject.toml, a uv lock file or another
    // manager's files count too, so that they are reported rather than silently skipped. A pyproject.toml with only
    // [tool.*] settings is not a project
    public bool IsProject(PyprojectManifest? manifest, string? problem)
    {
        return manifest?.HasProjectTable == true
            || manifest?.IsPoetry == true
            || problem is not null
            || this.RequirementFiles.Count != 0
            || this.HasUvLock
            || this.UnsupportedManifest is not null;
    }
}
