using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Credfeto.Build.Core.Jobs.Services;

/// <summary>
///     Masks secrets in process output before it is logged or returned: the values of secret-looking environment
///     variables, GitHub tokens, authorization headers, key=value secrets and credentials embedded in URLs.
/// </summary>
public sealed partial class OutputMasker : IOutputMasker
{
    public const string REPLACEMENT = "***";

    private const int MIN_SECRET_LENGTH = 8;

    private static readonly string[] SecretNameMarkers =
    [
        "TOKEN",
        "SECRET",
        "PASSWORD",
        "PASSWD",
        "APIKEY",
        "API_KEY",
        "ACCESS_KEY",
        "PRIVATE_KEY",
        "CREDENTIAL",
    ];

    private readonly string[] _secretValues;

    public OutputMasker(IReadOnlyDictionary<string, string> environment)
    {
        // Longest first, so a secret that contains another is masked whole
        this._secretValues =
        [
            .. environment
                .Where(static pair => IsSecretName(pair.Key) && pair.Value.Length >= MIN_SECRET_LENGTH)
                .Select(static pair => pair.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(static value => value.Length),
        ];
    }

    public string Mask(string line)
    {
        string masked = line;

        foreach (string secret in this._secretValues)
        {
            masked = masked.Replace(oldValue: secret, newValue: REPLACEMENT, comparisonType: StringComparison.Ordinal);
        }

        masked = GitHubToken().Replace(input: masked, replacement: REPLACEMENT);
        masked = AuthorizationHeader().Replace(input: masked, replacement: "${scheme} " + REPLACEMENT);
        masked = KeyValueSecret().Replace(input: masked, replacement: "${name}${separator}" + REPLACEMENT);

        return UrlCredentials().Replace(input: masked, replacement: "${scheme}" + REPLACEMENT + "@");
    }

    private static bool IsSecretName(string name)
    {
        return SecretNameMarkers.Any(marker => name.Contains(value: marker, comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(
        pattern: @"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})",
        options: RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex GitHubToken();

    [GeneratedRegex(
        pattern: @"\b(?<scheme>bearer|basic|token)\s+[A-Za-z0-9._~+/=-]{8,}",
        options: RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(
        pattern: @"\b(?<name>password|passwd|pwd|secret|token|api[_-]?key)(?<separator>\s*[=:]\s*)[^\s;,&""']+",
        options: RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex KeyValueSecret();

    [GeneratedRegex(
        pattern: @"(?<scheme>[A-Za-z][A-Za-z0-9+.-]*://)[^/\s:@]+:[^/\s@]+@",
        options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex UrlCredentials();
}
