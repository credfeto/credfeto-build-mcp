using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Python.Server.Detection;

// Reads the requirements a requirements*.txt lists: one per line, with comments, blank lines and pip options (-r,
// -c, -e, --index-url and the like) left out. Only the names are used, to tell whether pytest and coverage are declared
public static class RequirementsReader
{
    private const string PREFIX = "requirements";
    private const string EXTENSION = ".txt";
    private const int MAX_FILE_LENGTH = 1024 * 1024;

    public static bool IsRequirementsFile(string fileName)
    {
        return fileName.StartsWith(value: PREFIX, comparisonType: StringComparison.Ordinal)
            && fileName.EndsWith(value: EXTENSION, comparisonType: StringComparison.Ordinal);
    }

    // The requirements*.txt files directly in the directory, in name order
    public static IReadOnlyList<string> Find(string directory)
    {
        return
        [
            .. Directory
                .EnumerateFiles(path: directory, searchPattern: PREFIX + "*" + EXTENSION)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(IsRequirementsFile)
                .Order(StringComparer.Ordinal),
        ];
    }

    public static async ValueTask<(IReadOnlyList<string>? Requirements, string? Problem)> ReadAsync(
        string path,
        string displayPath,
        CancellationToken cancellationToken
    )
    {
        if (new FileInfo(path).Length > MAX_FILE_LENGTH)
        {
            return (null, displayPath + " is larger than 1 MiB");
        }

        string text = await File.ReadAllTextAsync(path: path, cancellationToken: cancellationToken);

        return (Parse(text), null);
    }

    public static IReadOnlyList<string> Parse(string text)
    {
        return
        [
            .. text.Split('\n')
                .Select(static line => WithoutComment(line).Trim())
                .Where(static line => line.Length != 0 && line[0] != '-'),
        ];
    }

    // A '#' at the start of a line or after whitespace starts a comment, as pip reads it
    private static string WithoutComment(string line)
    {
        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] == '#' && (index == 0 || char.IsWhiteSpace(line[index - 1])))
            {
                return line[..index];
            }
        }

        return line;
    }
}
