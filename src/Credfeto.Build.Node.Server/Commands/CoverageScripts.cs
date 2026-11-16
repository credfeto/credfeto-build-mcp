using System;
using System.IO;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Commands;

// The coverage script and report of a package. A package gets coverage only from a test:coverage script, or the
// script node.coverageScript names, and only when the package has that script: the server never invents a coverage
// command (no default vitest --coverage run, and no reporter flags), so what is measured is always what the
// repository itself configured (decided on credfeto/credfeto-orchestrator#1811)
public static class CoverageScripts
{
    // Vitest's cobertura reporter writes here by default, relative to the package's directory
    public const string DEFAULT_FILE = "coverage/cobertura-coverage.xml";

    public static string? For(NodePackage package, string? overrideScript)
    {
        string script = overrideScript ?? NodeScripts.TEST_COVERAGE;

        return package.HasScript(script) ? script : null;
    }

    public static string ReportPath(NodeRepository repository, NodePackage package)
    {
        string relative = (repository.CoverageFile ?? DEFAULT_FILE).Replace(
            oldChar: '/',
            newChar: Path.DirectorySeparatorChar
        );

        return StringComparer.Ordinal.Equals(x: package.RelativeDirectory, y: NodePackage.ROOT_DIRECTORY)
            ? Path.Combine(path1: repository.RepositoryRoot, path2: relative)
            : Path.Combine(
                path1: repository.RepositoryRoot,
                path2: package.RelativeDirectory.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar),
                path3: relative
            );
    }
}
