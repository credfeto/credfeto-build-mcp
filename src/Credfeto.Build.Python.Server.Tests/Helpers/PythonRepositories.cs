using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Testing;

namespace Credfeto.Build.Python.Server.Tests.Helpers;

// Writes Python projects for the tests
internal static class PythonRepositories
{
    public const string SAMPLE_PYPROJECT = """
        [project]
        name = "example"
        version = "0.1.0"
        requires-python = ">=3.11"
        dependencies = ["httpx>=0.27", "feedparser"]

        [project.optional-dependencies]
        dev = ["pytest", "coverage[toml]"]
        docs = ["sphinx"]

        [tool.pytest.ini_options]
        testpaths = ["tests"]
        """;

    // The sample project at the repository root
    public static ValueTask WriteAsync(TemporaryDirectory repository, CancellationToken cancellationToken)
    {
        return WriteAsync(
            repository: repository,
            relative: "pyproject.toml",
            content: SAMPLE_PYPROJECT,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask WriteAsync(
        TemporaryDirectory repository,
        string relative,
        string content,
        CancellationToken cancellationToken
    )
    {
        _ = await repository.WriteAsync(relative: relative, content: content, cancellationToken: cancellationToken);
    }
}
