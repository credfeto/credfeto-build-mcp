using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Tools;

// Detection stays strict: a test suite that nothing declares gets no job tools until it has a manifest. restore
// writes that manifest for it, at the repository root: a minimal pyproject.toml declaring pytest and coverage in a
// test extra and the test directories as pytest's testpaths (a hidden directory such as .github is only collected
// when named), or a requirements-dev.txt when a pyproject.toml with only tool settings is already there. It never
// overwrites a file, and the caller is told to commit it and add what is missing
public static class ManifestCreator
{
    public const string REQUIREMENTS_FILE = "requirements-dev.txt";

    private const string DEFAULT_NAME = "tests";

    private static readonly char[] UrlSeparators = ['/', ':'];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static async ValueTask<string> CreateAsync(
        string repositoryRoot,
        PythonTestSuite testSuite,
        CancellationToken cancellationToken
    )
    {
        bool hasPyproject = File.Exists(Path.Combine(path1: repositoryRoot, path2: PyprojectReader.FILE_NAME));
        string fileName = hasPyproject ? REQUIREMENTS_FILE : PyprojectReader.FILE_NAME;
        string content = hasPyproject
            ? "# Created by the build-python MCP server: the test tools the Python tests need. Commit it, and add\n# anything else the tests need.\npytest\ncoverage\n"
            : Pyproject(
                name: await NameAsync(repositoryRoot: repositoryRoot, cancellationToken: cancellationToken),
                testSuite: testSuite
            );

        await using FileStream stream = new(
            path: Path.Combine(path1: repositoryRoot, path2: fileName),
            mode: FileMode.CreateNew,
            access: FileAccess.Write,
            share: FileShare.None
        );
        await stream.WriteAsync(buffer: Utf8.GetBytes(content), cancellationToken: cancellationToken);

        return fileName;
    }

    public static string Note(string fileName)
    {
        return StringComparer.Ordinal.Equals(x: fileName, y: REQUIREMENTS_FILE)
            ? "There was no manifest for the Python tests, so restore created requirements-dev.txt at the repository root declaring pytest and coverage. Commit it, and add anything the tests need that is missing."
            : "There was no manifest for the Python tests, so restore created a minimal pyproject.toml at the repository root, declaring pytest and coverage in a test extra and the test directories as pytest testpaths. Commit it, and add anything that is missing, such as the dependencies of the code under test.";
    }

    public static string Pyproject(string name, PythonTestSuite testSuite)
    {
        StringBuilder builder = new();
        _ = builder
            .Append(
                "# Created by the build-python MCP server for Python tests that nothing declared. Commit it, and add\n"
            )
            .Append("# the dependencies of the code under test to [project].dependencies.\n")
            .Append("[project]\nname = \"")
            .Append(name)
            .Append("\"\nversion = \"0.0.0\"\ndependencies = []\n\n")
            .Append("[project.optional-dependencies]\ntest = [\n    \"pytest\",\n    \"coverage\",\n]\n\n")
            .Append("[tool.pytest.ini_options]\ntestpaths = [\n");

        foreach (string directory in testSuite.TestDirectories)
        {
            _ = builder.Append("    \"").Append(Escape(directory)).Append("\",\n");
        }

        return builder.Append("]\n").ToString();
    }

    // The repository's name from its origin remote, otherwise its directory, as a PEP 503 normal name
    private static async ValueTask<string> NameAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        string? origin = await GitCommand.TryRunAsync(
            repositoryRoot: repositoryRoot,
            arguments: ["config", "--get", "remote.origin.url"],
            cancellationToken: cancellationToken
        );
        string source = origin?.Trim() is { Length: > 0 } url ? LastSegment(url) : Path.GetFileName(repositoryRoot);

        return Normalise(source);
    }

    private static string LastSegment(string url)
    {
        string[] segments = url.TrimEnd('/').Split(UrlSeparators);
        string last = segments[^1];

        return last.EndsWith(value: ".git", comparisonType: StringComparison.Ordinal) ? last[..^4] : last;
    }

    public static string Normalise(string name)
    {
        string normal = RequirementName.Of(name) ?? string.Empty;

        return normal.Length == 0 ? DEFAULT_NAME : normal;
    }

    private static string Escape(string text)
    {
        return text.Replace(oldValue: "\\", newValue: "\\\\", comparisonType: StringComparison.Ordinal)
            .Replace(oldValue: "\"", newValue: "\\\"", comparisonType: StringComparison.Ordinal);
    }
}
