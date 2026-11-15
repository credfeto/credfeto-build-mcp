using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Credfeto.Build.Dotnet.Server.Packages;

// The packageSourceMapping NuGet restore applies: the sections of every config file merged, furthest file first, a
// <clear /> dropping what came before; the most specific matching pattern (an exact id, then the longest prefix*)
// picks the sources. dotnet package search ignores it, so the tool works it out itself
public sealed class PackageSourceMapping
{
    private const string SECTION = "packageSourceMapping";
    private const char WILDCARD = '*';

    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _patterns;

    private PackageSourceMapping(IReadOnlyDictionary<string, IReadOnlyList<string>> patterns)
    {
        this._patterns = patterns;
    }

    public bool Configured => this._patterns.Count != 0;

    // Closest file first, as dotnet nuget config paths lists them; a file that is missing or unreadable is skipped
    public static async ValueTask<PackageSourceMapping> LoadAsync(
        IReadOnlyList<string> configFiles,
        CancellationToken cancellationToken
    )
    {
        List<XDocument> documents = [];

        foreach (string file in configFiles.Reverse())
        {
            XDocument? document = await TryLoadAsync(path: file, cancellationToken: cancellationToken);

            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return FromDocuments(documents);
    }

    // Furthest file first
    public static PackageSourceMapping FromDocuments(IEnumerable<XDocument> documents)
    {
        Dictionary<string, List<string>> patterns = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (
            XElement section in documents.Select(static document => document.Root?.Element(SECTION)).OfType<XElement>()
        )
        {
            Merge(patterns: patterns, section: section);
        }

        Dictionary<string, IReadOnlyList<string>> mapped = [with(StringComparer.OrdinalIgnoreCase)];

        foreach ((string source, List<string> sourcePatterns) in patterns.Where(static pair => pair.Value.Count != 0))
        {
            mapped[source] = sourcePatterns;
        }

        return new(mapped);
    }

    // Whether restore would take the package from the source; with no mapping configured every source is used
    public bool Allows(string source, string packageId)
    {
        if (!this.Configured)
        {
            return true;
        }

        int best = this
            ._patterns.Values.SelectMany(static patterns => patterns)
            .Max(pattern => Specificity(pattern: pattern, packageId: packageId));

        return best >= 0
            && this._patterns.TryGetValue(key: source, value: out IReadOnlyList<string>? patterns)
            && patterns.Any(pattern => Specificity(pattern: pattern, packageId: packageId) == best);
    }

    private static void Merge(Dictionary<string, List<string>> patterns, XElement section)
    {
        foreach (XElement element in section.Elements())
        {
            if (StringComparer.Ordinal.Equals(x: element.Name.LocalName, y: "clear"))
            {
                patterns.Clear();

                continue;
            }

            if (
                !StringComparer.Ordinal.Equals(x: element.Name.LocalName, y: "packageSource")
                || element.Attribute("key")?.Value is not { Length: > 0 } key
            )
            {
                continue;
            }

            if (!patterns.TryGetValue(key: key, value: out List<string>? existing))
            {
                existing = [];
                patterns[key] = existing;
            }

            existing.AddRange(
                element
                    .Elements("package")
                    .Select(static package => package.Attribute("pattern")?.Value.Trim())
                    .OfType<string>()
                    .Where(static pattern => pattern.Length != 0)
            );
        }
    }

    // -1 for no match; an exact id beats any prefix, and a longer prefix beats a shorter one ("*" is length 0)
    private static int Specificity(string pattern, string packageId)
    {
        if (pattern[^1] != WILDCARD)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x: pattern, y: packageId) ? int.MaxValue : -1;
        }

        string prefix = pattern[..^1];

        return packageId.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)
            ? prefix.Length
            : -1;
    }

    private static async ValueTask<XDocument?> TryLoadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(
                path: path,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );

            return await XDocument.LoadAsync(
                stream: stream,
                options: LoadOptions.None,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
        {
            // NuGet itself reports a broken config file; the search result still stands without its mapping
            return null;
        }
    }
}
