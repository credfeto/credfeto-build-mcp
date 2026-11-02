using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Dotnet.Server.Detection;

public static partial class SolutionReader
{
    private const string PROJECT_EXTENSION = ".csproj";

    /// <summary>Lists the C# project paths in a .slnx or .sln file, relative to the solution's directory.</summary>
    public static async ValueTask<IReadOnlyList<string>> ReadProjectPathsAsync(string solutionPath, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> paths = solutionPath.EndsWith(value: ".slnx", comparisonType: StringComparison.OrdinalIgnoreCase)
            ? await ReadSlnxAsync(solutionPath: solutionPath, cancellationToken: cancellationToken)
            : await ReadSlnAsync(solutionPath: solutionPath, cancellationToken: cancellationToken);

        List<string> projects = [];

        foreach (string path in paths)
        {
            string normalised = RepositoryPaths.ToForwardSlashes(path);

            if (normalised.EndsWith(value: PROJECT_EXTENSION, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                projects.Add(normalised);
            }
        }

        return projects;
    }

    private static async ValueTask<IReadOnlyList<string>> ReadSlnxAsync(string solutionPath, CancellationToken cancellationToken)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            Async = true,
        };

        List<string> paths = [];

        await using FileStream stream = File.OpenRead(solutionPath);
        using XmlReader reader = XmlReader.Create(input: stream, settings: settings);

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element && StringComparer.Ordinal.Equals(x: reader.LocalName, y: "Project"))
            {
                string? path = reader.GetAttribute("Path");

                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }
        }

        return paths;
    }

    private static async ValueTask<IReadOnlyList<string>> ReadSlnAsync(string solutionPath, CancellationToken cancellationToken)
    {
        List<string> paths = [];

        await foreach (string line in File.ReadLinesAsync(path: solutionPath, cancellationToken: cancellationToken))
        {
            Match match = SlnProject().Match(line);

            if (match.Success)
            {
                paths.Add(match.Groups["path"].Value);
            }
        }

        return paths;
    }

    [GeneratedRegex(
        pattern: @"^Project\(""\{[^}]+\}""\)\s*=\s*""[^""]*""\s*,\s*""(?<path>[^""]+)""",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex SlnProject();
}
