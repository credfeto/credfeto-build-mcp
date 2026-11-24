using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Credfeto.Build.Python.Server.Detection;

// The distribution name a PEP 508 requirement names, normalised as PEP 503 does (lower case, every run of '-', '_'
// and '.' made one '-'), so "PyTest>=8" and "pytest" name the same distribution
public static class RequirementName
{
    public const string PYTEST = "pytest";
    public const string COVERAGE = "coverage";

    // pytest-cov depends on coverage, so declaring it declares coverage too
    private const string PYTEST_COV = "pytest-cov";

    public static string? Of(string requirement)
    {
        ReadOnlySpan<char> text = requirement.AsSpan().TrimStart();
        int length = 0;

        while (length < text.Length && IsNameCharacter(text[length]))
        {
            length++;
        }

        return length == 0 || !char.IsAsciiLetterOrDigit(text[0]) ? null : Normalise(text[..length]);
    }

    public static bool DeclaresPytest(IEnumerable<string> names)
    {
        return names.Contains(value: PYTEST, comparer: StringComparer.Ordinal);
    }

    public static bool DeclaresCoverage(IEnumerable<string> names)
    {
        return names.Any(static name =>
            StringComparer.Ordinal.Equals(x: name, y: COVERAGE) || StringComparer.Ordinal.Equals(x: name, y: PYTEST_COV)
        );
    }

    public static IReadOnlyList<string> Names(IEnumerable<string> requirements)
    {
        return [.. requirements.Select(Of).OfType<string>().Distinct(StringComparer.Ordinal)];
    }

    private static bool IsNameCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.';
    }

    private static string Normalise(ReadOnlySpan<char> name)
    {
        StringBuilder builder = new(name.Length);
        bool separator = false;

        foreach (char character in name)
        {
            if (character is '-' or '_' or '.')
            {
                separator = true;

                continue;
            }

            if (separator && builder.Length != 0)
            {
                _ = builder.Append('-');
            }

            separator = false;
            _ = builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
