using System;
using System.Collections.Generic;
using System.Globalization;

namespace Credfeto.Build.Dotnet.Server.Packages;

// NuGet version order: up to four numeric parts, a release above its prereleases, dotted prerelease labels compared
// as SemVer 2 (numeric identifiers numerically and below text ones, text case-insensitively), metadata ignored
public sealed class NuGetVersionComparer : IComparer<string>
{
    private const int NUMERIC_PARTS = 4;

    private static readonly char[] Separators = ['.'];

    public static readonly NuGetVersionComparer Instance = new();

    private NuGetVersionComparer() { }

    public int Compare(string? x, string? y)
    {
        long[]? left = NumericParts(x);
        long[]? right = NumericParts(y);

        if (left is null || right is null)
        {
            return CompareUnparsed(x: x, y: y, left: left, right: right);
        }

        for (int index = 0; index < NUMERIC_PARTS; index++)
        {
            int compared = left[index].CompareTo(right[index]);

            if (compared != 0)
            {
                return compared;
            }
        }

        return CompareLabels(x: Label(x), y: Label(y));
    }

    public static bool IsPrerelease(string version)
    {
        return Label(version).Length != 0;
    }

    private static int CompareUnparsed(string? x, string? y, long[]? left, long[]? right)
    {
        if (left is null && right is null)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(x: x, y: y);
        }

        // A version that cannot be read sorts below every version that can
        return left is null ? -1 : 1;
    }

    private static int CompareLabels(string x, string y)
    {
        if (x.Length == 0 || y.Length == 0)
        {
            // A release (no label) is above its prereleases
            return y.Length.CompareTo(x.Length);
        }

        string[] left = x.Split(Separators);
        string[] right = y.Split(Separators);

        for (int index = 0; index < Math.Min(val1: left.Length, val2: right.Length); index++)
        {
            int compared = CompareIdentifiers(x: left[index], y: right[index]);

            if (compared != 0)
            {
                return compared;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int CompareIdentifiers(string x, string y)
    {
        bool leftNumeric = long.TryParse(
            s: x,
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out long left
        );
        bool rightNumeric = long.TryParse(
            s: y,
            style: NumberStyles.None,
            provider: CultureInfo.InvariantCulture,
            result: out long right
        );

        if (leftNumeric && rightNumeric)
        {
            return left.CompareTo(right);
        }

        if (leftNumeric != rightNumeric)
        {
            return leftNumeric ? -1 : 1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(x: x, y: y);
    }

    private static string WithoutMetadata(string version)
    {
        int plus = version.IndexOf('+', StringComparison.Ordinal);

        return plus < 0 ? version : version[..plus];
    }

    private static string Label(string? version)
    {
        string bare = WithoutMetadata(version ?? string.Empty);
        int dash = bare.IndexOf('-', StringComparison.Ordinal);

        return dash < 0 ? string.Empty : bare[(dash + 1)..];
    }

    private static long[]? NumericParts(string? version)
    {
        string bare = WithoutMetadata(version ?? string.Empty);
        int dash = bare.IndexOf('-', StringComparison.Ordinal);
        string[] parts = (dash < 0 ? bare : bare[..dash]).Split(Separators);

        if (parts.Length > NUMERIC_PARTS)
        {
            return null;
        }

        long[] numbers = new long[NUMERIC_PARTS];

        for (int index = 0; index < parts.Length; index++)
        {
            if (
                !long.TryParse(
                    s: parts[index],
                    style: NumberStyles.None,
                    provider: CultureInfo.InvariantCulture,
                    result: out numbers[index]
                )
            )
            {
                return null;
            }
        }

        return numbers;
    }
}
