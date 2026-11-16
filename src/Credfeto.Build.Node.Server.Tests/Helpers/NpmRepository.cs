using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Testing;

namespace Credfeto.Build.Node.Server.Tests.Helpers;

// Writes npm repositories for the tests: a package.json and, unless told otherwise, its package-lock.json
internal static class NpmRepository
{
    public const string SAMPLE_MANIFEST = """
        {
          "name": "example",
          "scripts": { "build": "tsc", "test": "vitest run", "start": "node dist/index.js" }
        }
        """;

    public const string NPM_LOCK_FILE = "package-lock.json";

    // The sample package at the repository root, with an npm lock file
    public static ValueTask WriteAsync(TemporaryDirectory repository, CancellationToken cancellationToken)
    {
        return WriteAsync(
            repository: repository,
            directory: "",
            manifest: SAMPLE_MANIFEST,
            lockFile: NPM_LOCK_FILE,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask WriteAsync(
        TemporaryDirectory repository,
        string directory,
        string manifest,
        string? lockFile,
        CancellationToken cancellationToken
    )
    {
        _ = await repository.WriteAsync(
            relative: Path.Combine(path1: directory, path2: "package.json"),
            content: manifest,
            cancellationToken: cancellationToken
        );

        if (lockFile is not null)
        {
            _ = await repository.WriteAsync(
                relative: Path.Combine(path1: directory, path2: lockFile),
                content: "{}",
                cancellationToken: cancellationToken
            );
        }
    }
}
