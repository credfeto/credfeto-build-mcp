using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Node.Server.Detection;

// Collects the install roots, their packages (workspaces included) and the directories worth watching. The first
// problem found stops the scan: the repository is then reported as not supported, with that problem as the reason
internal sealed class PackageScan
{
    private readonly List<NodeInstallRoot> _installRoots = [];
    private readonly List<NodePackage> _packages = [];
    private readonly HashSet<string> _packageDirectories = [with(StringComparer.Ordinal)];
    private readonly List<string> _watchedDirectories = [];

    public PackageScan(string repositoryRoot)
    {
        this.RepositoryRoot = repositoryRoot;
    }

    public string RepositoryRoot { get; }

    public string? Problem { get; private set; }

    public IReadOnlyList<NodeInstallRoot> InstallRoots => this._installRoots;

    public IReadOnlyList<NodePackage> Packages => this._packages;

    public IReadOnlyList<string> WatchedDirectories => this._watchedDirectories;

    public async ValueTask AddInstallRootAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        if (this.Problem is not null)
        {
            return;
        }

        string directory = this.FullPath(relativeDirectory);
        PackageManifest? manifest = await this.ReadPackageAsync(
            relativeDirectory: relativeDirectory,
            cancellationToken: cancellationToken
        );

        if (manifest is null)
        {
            return;
        }

        (string? lockFile, string? problem) = LockFiles.Find(
            directory: directory,
            displayDirectory: relativeDirectory,
            manifest: manifest
        );

        if (lockFile is null)
        {
            this.Problem = problem;

            return;
        }

        this._installRoots.Add(new(RelativeDirectory: relativeDirectory, LockFile: Join(relativeDirectory, lockFile)));
        await this.AddWorkspacesAsync(
            installRoot: relativeDirectory,
            manifest: manifest,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask AddWorkspacesAsync(
        string installRoot,
        PackageManifest manifest,
        CancellationToken cancellationToken
    )
    {
        foreach (string pattern in manifest.Workspaces)
        {
            if (WorkspacePattern.Expand(installRoot: this.FullPath(installRoot), pattern: pattern) is not { } expanded)
            {
                continue;
            }

            this._watchedDirectories.Add(Join(installRoot, expanded.WatchDirectory));

            foreach (string directory in expanded.Directories)
            {
                string relative = Join(installRoot, directory);
                this._watchedDirectories.Add(relative);
                _ = await this.ReadPackageAsync(relativeDirectory: relative, cancellationToken: cancellationToken);
            }
        }
    }

    // Records the package in the directory, if it has a package.json that can be read; null when it has none (or
    // the package was already recorded through another route) or when it is unusable, which sets the problem
    private async ValueTask<PackageManifest?> ReadPackageAsync(
        string relativeDirectory,
        CancellationToken cancellationToken
    )
    {
        string path = Path.Combine(path1: this.FullPath(relativeDirectory), path2: PackageManifestReader.FILE_NAME);

        if (this.Problem is not null || !File.Exists(path) || !this._packageDirectories.Add(relativeDirectory))
        {
            return null;
        }

        (PackageManifest? manifest, string? problem) = await PackageManifestReader.ReadAsync(
            path: path,
            displayPath: Join(relativeDirectory, PackageManifestReader.FILE_NAME),
            cancellationToken: cancellationToken
        );

        if (manifest is null)
        {
            this.Problem = problem;

            return null;
        }

        this._packages.Add(
            new(
                Name: manifest.Name ?? relativeDirectory,
                RelativeDirectory: relativeDirectory,
                Scripts: manifest.Scripts
            )
        );

        return manifest;
    }

    private string FullPath(string relativeDirectory)
    {
        return StringComparer.Ordinal.Equals(x: relativeDirectory, y: NodePackage.ROOT_DIRECTORY)
            ? this.RepositoryRoot
            : Path.Combine(path1: this.RepositoryRoot, path2: relativeDirectory);
    }

    // Joins repository-relative paths with forward slashes; "." is the repository root
    private static string Join(string directory, string relative)
    {
        if (StringComparer.Ordinal.Equals(x: directory, y: NodePackage.ROOT_DIRECTORY))
        {
            return relative.Length == 0 ? NodePackage.ROOT_DIRECTORY : relative;
        }

        return relative.Length == 0 ? directory : directory + "/" + relative;
    }
}
