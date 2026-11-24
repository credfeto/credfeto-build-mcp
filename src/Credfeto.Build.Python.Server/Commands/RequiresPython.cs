using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Credfeto.Build.Python.Server.Commands;

// Evaluates a requires-python specifier set (PEP 440: ==, !=, <, <=, >, >=, ~= and === with release numbers, and
// == or != with a trailing .*) against the interpreter's version. Null when a clause cannot be read, so a
// specifier the server does not understand never blocks a run
public static partial class RequiresPython
{
    public static bool? Allows(string specifiers, PythonVersion version)
    {
        bool allowed = true;

        foreach (
            string clause in specifiers
                .Split(',')
                .Select(static clause => clause.Trim())
                .Where(static clause => clause.Length != 0)
        )
        {
            Match match = Clause().Match(clause);

            if (!match.Success)
            {
                return null;
            }

            int[] wanted =
            [
                .. match
                    .Groups["version"]
                    .Value.Split('.')
                    .Select(static part => int.Parse(s: part, provider: CultureInfo.InvariantCulture)),
            ];
            allowed &= Holds(
                operation: match.Groups["operator"].Value,
                wanted: wanted,
                wildcard: match.Groups["wildcard"].Success,
                actual: version.Release()
            );
        }

        return allowed;
    }

    private static bool Holds(string operation, int[] wanted, bool wildcard, int[] actual)
    {
        int comparison = PythonVersion.Compare(left: actual, right: wanted);

        return operation switch
        {
            "==" or "===" => wildcard ? HasPrefix(actual: actual, prefix: wanted) : comparison == 0,
            "!=" => wildcard ? !HasPrefix(actual: actual, prefix: wanted) : comparison != 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            _ => wanted.Length >= 2 && comparison >= 0 && HasPrefix(actual: actual, prefix: wanted[..^1]),
        };
    }

    private static bool HasPrefix(int[] actual, int[] prefix)
    {
        return prefix
            .Select((part, index) => index < actual.Length ? actual[index] == part : part == 0)
            .All(static same => same);
    }

    [GeneratedRegex(
        pattern: @"^(?<operator>~=|===|==|!=|<=|>=|<|>)\s*(?<version>\d{1,5}(?:\.\d{1,5}){0,3})(?<wildcard>\.\*)?$",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Clause();
}
