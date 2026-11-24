using System.Collections.Generic;

namespace Credfeto.Build.Python.Server.Detection;

// The files that make a directory a Python project, or say which tool manages it
public static class ManifestFiles
{
    public const string UV_LOCK = "uv.lock";

    // Lock files and manifests of the managers the server does not support, with the manager each one means
    public static IReadOnlyList<UnsupportedManifest> Unsupported { get; } =
    [
        new(FileName: "poetry.lock", Manager: "poetry"),
        new(FileName: "Pipfile", Manager: "pipenv"),
        new(FileName: "Pipfile.lock", Manager: "pipenv"),
        new(FileName: "pdm.lock", Manager: "pdm"),
    ];

    // The exact file names whose changes can change what detection finds (requirements*.txt is matched by extension)
    public static IReadOnlyList<string> WatchedFileNames { get; } =
    [PyprojectReader.FILE_NAME, UV_LOCK, "poetry.lock", "Pipfile", "Pipfile.lock", "pdm.lock"];
}
