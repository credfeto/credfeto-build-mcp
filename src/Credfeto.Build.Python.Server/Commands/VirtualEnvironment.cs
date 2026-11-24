using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Commands;

// A project's own isolated virtual environment, <project>/.venv as the python instructions require, and the stamp
// that says what it was installed from: the SHA-256 of the installer, the interpreter and every manifest and lock
// file. A job reinstalls only when the stamp is missing or different
[DebuggerDisplay("{Directory}")]
public sealed record VirtualEnvironment(string ProjectDirectory, string Directory)
{
    public const string DIRECTORY_NAME = ".venv";
    public const string STAMP_FILE = ".credfeto-build-python-stamp";

    public string Python => Path.Combine(path1: this.Directory, path2: "bin", path3: "python");

    public string BinDirectory => Path.Combine(path1: this.Directory, path2: "bin");

    public string StampPath => Path.Combine(path1: this.Directory, path2: STAMP_FILE);

    // coverage.py's data file and the Cobertura report made from it: server-owned, inside the git-ignored venv, so a
    // run never leaves a .coverage file in the repository
    public string CoverageDataPath => Path.Combine(path1: this.Directory, path2: ".credfeto-build-python-coverage");

    public string CoverageReportPath =>
        Path.Combine(path1: this.Directory, path2: ".credfeto-build-python-coverage.xml");

    public static VirtualEnvironment For(string repositoryRoot, PythonProject project)
    {
        string projectDirectory = ProjectReader.FullPath(
            repositoryRoot: repositoryRoot,
            relativeDirectory: project.RelativeDirectory
        );

        return new(
            ProjectDirectory: projectDirectory,
            Directory: Path.Combine(path1: projectDirectory, path2: DIRECTORY_NAME)
        );
    }

    // Where the installed packages are, so pylint (which is not installed in the venv) can see them
    public string SitePackages(PythonVersion version)
    {
        return Path.Combine(this.Directory, "lib", "python" + version.MajorMinor, "site-packages");
    }

    public static async ValueTask<string> StampAsync(
        VirtualEnvironment environment,
        PythonProject project,
        string interpreter,
        PythonVersion version,
        CancellationToken cancellationToken
    )
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(
            hash: hash,
            text: "credfeto-build-python 1\n" + project.Installer + "\n" + interpreter + "\n" + version.Text + "\n"
        );

        foreach (string manifest in project.Manifests.Order(StringComparer.Ordinal))
        {
            Append(hash: hash, text: manifest + "\n");
            string path = Path.Combine(path1: environment.ProjectDirectory, path2: manifest);

            if (File.Exists(path))
            {
                hash.AppendData(await File.ReadAllBytesAsync(path: path, cancellationToken: cancellationToken));
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public async ValueTask<bool> IsCurrentAsync(string stamp, CancellationToken cancellationToken)
    {
        if (!File.Exists(this.Python) || !File.Exists(this.StampPath))
        {
            return false;
        }

        string written = await File.ReadAllTextAsync(path: this.StampPath, cancellationToken: cancellationToken);

        return StringComparer.Ordinal.Equals(x: written.Trim(), y: stamp);
    }

    private static void Append(IncrementalHash hash, string text)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(text));
    }
}
