using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Credfeto.Build.Node.Server.Testing;

// Reads "<number> <word>" parts such as "3 passed" out of a runner's summary line
internal static partial class SummaryParts
{
    public static int Count(string text, string name)
    {
        return Part()
            .Matches(text)
            .Where(part => StringComparer.Ordinal.Equals(x: part.Groups["name"].Value, y: name))
            .Select(static part => int.Parse(s: part.Groups["value"].Value, provider: CultureInfo.InvariantCulture))
            .FirstOrDefault();
    }

    [GeneratedRegex(
        pattern: @"(?<value>\d{1,9})\s+(?<name>[a-z]+)",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Part();
}
