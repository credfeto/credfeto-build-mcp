using System.Text.RegularExpressions;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

// The package id and version the package tools accept. Both are fixed patterns, so only a registry name and a
// version, range or tag reach npm: no URL, path, git or alias specification
internal static partial class NpmPackageArguments
{
    // npm's rules for a package name: lower case, at most 214 characters, optionally scoped
    public const string PACKAGE_ID_PATTERN = "^(?:@[a-z0-9][a-z0-9._~-]{0,100}/)?[a-z0-9][a-z0-9._~-]{0,100}$";

    // A version, a range (^1.2.3, ~1.2, >=1 <2 written without spaces) or a dist-tag such as latest or next
    public const string VERSION_PATTERN = "^[0-9A-Za-z^~<>=*.+-]{1,64}$";

    public static readonly SchemaProperty PackageId = new(
        Name: "package_id",
        Description: "The npm package name, for example vitest or @types/node",
        Values: null,
        Pattern: PACKAGE_ID_PATTERN,
        DefaultValue: null,
        Required: true
    );

    public static string Id(ToolArguments arguments)
    {
        return arguments.GetMatching(property: PackageId, pattern: PackageIdRegex());
    }

    public static string? Version(ToolArguments arguments, SchemaProperty property)
    {
        return arguments.GetChoice(property: property, allowed: null) is null
            ? null
            : arguments.GetMatching(property: property, pattern: VersionRegex());
    }

    public static SchemaProperty VersionProperty(string description)
    {
        return new(
            Name: "version",
            Description: description,
            Values: null,
            Pattern: VERSION_PATTERN,
            DefaultValue: null,
            Required: false
        );
    }

    [GeneratedRegex(
        pattern: PACKAGE_ID_PATTERN,
        options: RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex PackageIdRegex();

    [GeneratedRegex(pattern: VERSION_PATTERN, options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionRegex();
}
