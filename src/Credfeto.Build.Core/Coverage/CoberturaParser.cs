using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.Build.Core.Coverage.Exceptions;

namespace Credfeto.Build.Core.Coverage;

public static class CoberturaParser
{
    private const long MAX_DOCUMENT_CHARACTERS = 512L * 1024 * 1024;

    public static async ValueTask<CoberturaReport> ParseFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);

        return await ParseAsync(stream: stream, cancellationToken: cancellationToken);
    }

    public static async ValueTask<CoberturaReport> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        // DTDs and external entities are refused: a coverage file is build output, but it is still parsed as
        // untrusted input
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = MAX_DOCUMENT_CHARACTERS,
            Async = true,
        };

        try
        {
            using XmlReader reader = XmlReader.Create(input: stream, settings: settings);

            return await ReadAsync(reader: reader, cancellationToken: cancellationToken);
        }
        catch (XmlException exception)
        {
            throw new InvalidCoberturaException(message: "The Cobertura report is not well-formed XML", innerException: exception);
        }
    }

    private static async ValueTask<CoberturaReport> ReadAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        Dictionary<string, Dictionary<SourceLine, bool>> packages = new(StringComparer.Ordinal);
        bool sawRoot = false;
        Dictionary<SourceLine, bool>? currentPackage = null;
        string currentFile = string.Empty;

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.LocalName)
            {
                case "coverage":
                    sawRoot = true;

                    break;
                case "package":
                    currentPackage = GetOrAddPackage(packages: packages, name: RequiredAttribute(reader: reader, name: "name"));

                    break;
                case "class":
                    currentFile = reader.GetAttribute("filename") ?? string.Empty;

                    break;
                case "line" when currentPackage is not null:
                    RecordLine(reader: reader, lines: currentPackage, file: currentFile);

                    break;
            }
        }

        if (!sawRoot)
        {
            throw new InvalidCoberturaException("The document has no Cobertura coverage element");
        }

        List<CoberturaPackage> result = [];

        foreach ((string name, Dictionary<SourceLine, bool> lines) in packages)
        {
            result.Add(new(Name: name, Lines: lines));
        }

        return new(result);
    }

    private static Dictionary<SourceLine, bool> GetOrAddPackage(
        Dictionary<string, Dictionary<SourceLine, bool>> packages,
        string name
    )
    {
        if (!packages.TryGetValue(key: name, value: out Dictionary<SourceLine, bool>? lines))
        {
            lines = [];
            packages.Add(key: name, value: lines);
        }

        return lines;
    }

    private static void RecordLine(XmlReader reader, Dictionary<SourceLine, bool> lines, string file)
    {
        int number = ParseInteger(reader: reader, name: "number");
        long hits = ParseLong(reader: reader, name: "hits");
        SourceLine line = new(File: file, Number: number);

        // Method-level <line> elements repeat the class-level ones, so a line counts once and is covered if any
        // entry for it was hit
        lines[line] = lines.TryGetValue(key: line, value: out bool covered) && covered || hits > 0;
    }

    private static string RequiredAttribute(XmlReader reader, string name)
    {
        string? value = reader.GetAttribute(name);

        return string.IsNullOrEmpty(value)
            ? throw new InvalidCoberturaException("A Cobertura " + reader.LocalName + " element has no " + name + " attribute")
            : value;
    }

    private static int ParseInteger(XmlReader reader, string name)
    {
        return int.TryParse(
            s: RequiredAttribute(reader: reader, name: name),
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out int value
        )
            ? value
            : throw new InvalidCoberturaException("A Cobertura line has a non-numeric " + name + " attribute");
    }

    private static long ParseLong(XmlReader reader, string name)
    {
        return long.TryParse(
            s: RequiredAttribute(reader: reader, name: name),
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out long value
        )
            ? value
            : throw new InvalidCoberturaException("A Cobertura line has a non-numeric " + name + " attribute");
    }
}
