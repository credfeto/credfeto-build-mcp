using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Coverage;

public static class CoverageFile
{
    public const string FILE_NAME = "COVERAGE.md";

    private const string HEADING_PREFIX = "## ";
    private const string FOOTER_RULE = "---";
    private const string FOOTER_PREFIX = "Captured at commit `";

    // How long a writer waits for another server's write of the same file to finish, and how often it looks
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockRetry = TimeSpan.FromMilliseconds(100);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // Replaces (or inserts) one language's section and the commit line of the file at path, under an exclusive lock
    // held from the read to the write, so two servers finishing at once can never lose each other's section
    public static async ValueTask WriteSectionAsync(
        string path,
        CoverageLanguage language,
        string section,
        string headSha,
        DateOnly capturedOn,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        await using FileStream stream = await OpenExclusiveAsync(
            path: path,
            mode: FileMode.OpenOrCreate,
            timeProvider: timeProvider,
            cancellationToken: cancellationToken
        );

        string existing = await ReadAllAsync(stream: stream, cancellationToken: cancellationToken);

        string merged = Merge(
            existing: existing.Length == 0 ? null : existing,
            language: language,
            section: section,
            headSha: headSha,
            capturedOn: capturedOn
        );

        await ReplaceContentAsync(stream: stream, text: merged, cancellationToken: cancellationToken);
    }

    // Replaces (or adds) one component's row of a language's table and sums Overall again from every row's line
    // counts, under the same lock as WriteSectionAsync. Only a file that already has the language's table is changed:
    // the other components' figures can only come from it
    public static async ValueTask<CoverageRowUpdate> UpdateRowAsync(
        string path,
        CoverageLanguage language,
        CoverageRow row,
        string headSha,
        DateOnly capturedOn,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        FileStream stream;

        try
        {
            stream = await OpenExclusiveAsync(
                path: path,
                mode: FileMode.Open,
                timeProvider: timeProvider,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CoverageRowUpdate.NoTable;
        }

        await using (stream)
        {
            string existing = await ReadAllAsync(stream: stream, cancellationToken: cancellationToken);
            CoverageRowUpdate update = UpdateRow(
                existing: existing,
                language: language,
                row: row,
                headSha: headSha,
                capturedOn: capturedOn
            );

            if (update.Text is { } text)
            {
                await ReplaceContentAsync(stream: stream, text: text, cancellationToken: cancellationToken);
            }

            return update;
        }
    }

    public static CoverageRowUpdate UpdateRow(
        string existing,
        CoverageLanguage language,
        CoverageRow row,
        string headSha,
        DateOnly capturedOn
    )
    {
        if (
            FindSection(text: existing, language: language) is not { } section
            || !CoverageTable.TryParse(
                section: section,
                rows: out IReadOnlyList<CoverageRow> rows,
                overall: out CoverageRow? overall
            )
        )
        {
            return CoverageRowUpdate.NoTable;
        }

        // Ordered as a full run orders its rows, so either run writes the same table for the same figures
        CoverageRow[] updated =
        [
            .. rows.Where(existingRow => !StringComparer.Ordinal.Equals(x: existingRow.Name, y: row.Name))
                .Append(row)
                .OrderBy(keySelector: static component => component.Name, comparer: StringComparer.Ordinal),
        ];
        string[] withoutCounts =
        [
            .. updated.Where(static component => !component.HasCounts).Select(static component => component.Name),
        ];

        // A row with no counts cannot be summed, so Overall is kept as it was rather than guessed
        CoverageRow total =
            withoutCounts.Length == 0
                ? CoverageRow.Measured(
                    name: language.OverallLabel,
                    covered: updated.Sum(static component => component.Covered ?? 0),
                    coverable: updated.Sum(static component => component.Coverable ?? 0)
                )
                : overall;

        string text = Merge(
            existing: existing,
            language: language,
            section: CoverageTable.Render(language: language, rows: updated, overall: total),
            headSha: headSha,
            capturedOn: capturedOn
        );

        return new(
            Outcome: withoutCounts.Length == 0
                ? CoverageRowOutcome.OverallRecomputed
                : CoverageRowOutcome.OverallUnchanged,
            RowsWithoutCounts: withoutCounts,
            Text: text
        );
    }

    private static async ValueTask<string> ReadAllAsync(FileStream stream, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(
            stream: stream,
            encoding: Utf8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true
        );

        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async ValueTask ReplaceContentAsync(
        FileStream stream,
        string text,
        CancellationToken cancellationToken
    )
    {
        stream.SetLength(0);
        await stream.WriteAsync(buffer: Utf8.GetBytes(text), cancellationToken: cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async ValueTask<FileStream> OpenExclusiveAsync(
        string path,
        FileMode mode,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset giveUpAt = timeProvider.GetUtcNow() + LockWait;

        while (true)
        {
            try
            {
                return new(
                    path: path,
                    mode: mode,
                    access: FileAccess.ReadWrite,
                    share: FileShare.None,
                    bufferSize: 4096,
                    useAsync: true
                );
            }
            catch (IOException exception)
                when (exception is not FileNotFoundException and not DirectoryNotFoundException
                    && timeProvider.GetUtcNow() < giveUpAt
                )
            {
                // Another writer holds the file; anything else that is not a lock is not worth waiting for
                await Task.Delay(delay: LockRetry, timeProvider: timeProvider, cancellationToken: cancellationToken);
            }
        }
    }

    public static string Merge(
        string? existing,
        CoverageLanguage language,
        string section,
        string headSha,
        DateOnly capturedOn
    )
    {
        string footer =
            FOOTER_PREFIX
            + headSha
            + "` on "
            + capturedOn.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture)
            + ".";

        if (string.IsNullOrEmpty(existing))
        {
            return "# Coverage\n\nGenerated by the AI Coverage phase. Do not edit by hand.\n\n"
                + Normalise(text: section, newLine: "\n")
                + "\n---\n\n"
                + footer
                + "\n";
        }

        string newLine = existing.Contains(value: "\r\n", comparisonType: StringComparison.Ordinal) ? "\r\n" : "\n";
        string merged = ReplaceSection(
            existing: existing,
            language: language,
            section: Normalise(text: section, newLine: newLine),
            newLine: newLine
        );

        return ReplaceFooter(text: merged, footer: footer, newLine: newLine);
    }

    private static string Normalise(string text, string newLine)
    {
        return text.TrimEnd('\r', '\n').ReplaceLineEndings(newLine) + newLine;
    }

    private static string ReplaceSection(string existing, CoverageLanguage language, string section, string newLine)
    {
        (int start, int end) = SectionBounds(text: existing, language: language);

        if (start >= 0)
        {
            // The section runs to the next heading or the footer rule, and only that range is replaced
            return end < 0 ? existing[..start] + section : existing[..start] + section + newLine + existing[end..];
        }

        int insertAt = FindLine(
            text: existing,
            from: AfterEarlierSections(text: existing, order: CoverageLanguage.OrderOf(language.Heading)),
            predicate: IsBoundary
        );

        return insertAt < 0
            ? WithTrailingNewLine(text: existing, newLine: newLine) + newLine + section
            : existing[..insertAt] + section + newLine + existing[insertAt..];
    }

    private static string? FindSection(string text, CoverageLanguage language)
    {
        (int start, int end) = SectionBounds(text: text, language: language);

        if (start < 0)
        {
            return null;
        }

        return end < 0 ? text[start..] : text[start..end];
    }

    // Where a language's section starts (its heading) and ends (the next heading or the footer rule, or -1 at the end
    // of the file); the start is -1 when the file has no such section
    private static (int Start, int End) SectionBounds(string text, CoverageLanguage language)
    {
        string heading = HEADING_PREFIX + language.Heading;
        int start = FindLine(text: text, from: 0, predicate: line =>
            StringComparer.Ordinal.Equals(x: line.TrimEnd(), y: heading)
        );

        return start < 0
            ? (-1, -1)
            : (start, FindLine(text: text, from: NextLine(text: text, start: start), predicate: IsBoundary));
    }

    // Where the search for a new section's place starts: just after the heading of the last section the documented
    // order puts before it, so the new section lands after that section and before the next heading or the footer
    private static int AfterEarlierSections(string text, int order)
    {
        int from = 0;

        for (int start = 0; start < text.Length; start = NextLine(text: text, start: start))
        {
            string line = text[start..ContentEnd(text: text, start: start)];

            if (
                line.StartsWith(value: HEADING_PREFIX, comparisonType: StringComparison.Ordinal)
                && CoverageLanguage.OrderOf(line[HEADING_PREFIX.Length..].TrimEnd()) < order
            )
            {
                from = NextLine(text: text, start: start);
            }
        }

        return from;
    }

    private static string ReplaceFooter(string text, string footer, string newLine)
    {
        int start = FindLine(text: text, from: 0, predicate: IsFooter);

        if (start >= 0)
        {
            return text[..start] + footer + text[ContentEnd(text: text, start: start)..];
        }

        string ended = WithTrailingNewLine(text: text, newLine: newLine);

        return FindLine(text: text, from: 0, predicate: IsRule) >= 0
            ? ended + newLine + footer + newLine
            : ended + newLine + FOOTER_RULE + newLine + newLine + footer + newLine;
    }

    private static string WithTrailingNewLine(string text, string newLine)
    {
        return text.Length != 0 && text[^1] == '\n' ? text : text + newLine;
    }

    private static int FindLine(string text, int from, Func<string, bool> predicate)
    {
        for (int start = from; start < text.Length; start = NextLine(text: text, start: start))
        {
            if (predicate(text[start..ContentEnd(text: text, start: start)]))
            {
                return start;
            }
        }

        return -1;
    }

    private static int NextLine(string text, int start)
    {
        int newLine = LineFeed(text: text, start: start);

        return newLine < 0 ? text.Length : newLine + 1;
    }

    private static int ContentEnd(string text, int start)
    {
        int newLine = LineFeed(text: text, start: start);

        if (newLine < 0)
        {
            return text.Length;
        }

        return newLine > start && text[newLine - 1] == '\r' ? newLine - 1 : newLine;
    }

    private static int LineFeed(string text, int start)
    {
        int offset = text.AsSpan(start).IndexOf('\n');

        return offset < 0 ? -1 : start + offset;
    }

    private static bool IsBoundary(string line)
    {
        return line.StartsWith(value: HEADING_PREFIX, comparisonType: StringComparison.Ordinal) || IsRule(line);
    }

    private static bool IsRule(string line)
    {
        return StringComparer.Ordinal.Equals(x: line.TrimEnd(), y: FOOTER_RULE);
    }

    private static bool IsFooter(string line)
    {
        return line.StartsWith(value: FOOTER_PREFIX, comparisonType: StringComparison.Ordinal);
    }
}
