using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Dotnet.Server.Models;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Packages;

public static class PackageVersionsReport
{
    // Only the sources that list the package; the overall latest versions come from the sources restore would use
    public static PackageVersionsResponse Create(
        string packageId,
        bool includePrerelease,
        int take,
        PackageSearchResult search,
        PackageSourceMapping mapping
    )
    {
        PackageSourceVersions[] sources =
        [
            .. search
                .Sources.Select(source =>
                    Describe(
                        source: source,
                        packageId: packageId,
                        includePrerelease: includePrerelease,
                        take: take,
                        mapping: mapping
                    )
                )
                .Where(static source => source.VersionCount != 0),
        ];

        if (sources.Length == 0)
        {
            throw NotFound(packageId: packageId, search: search);
        }

        string[] restorable =
        [
            .. sources
                .Where(static source => source.UsedByRestore)
                .SelectMany(static source => new[] { source.LatestStable, source.LatestPrerelease })
                .OfType<string>(),
        ];

        return new(
            PackageId: packageId,
            IncludePrerelease: includePrerelease,
            SourceMappingConfigured: mapping.Configured,
            LatestStable: Latest(versions: restorable, prerelease: false),
            LatestPrerelease: Latest(versions: restorable, prerelease: true),
            Sources: sources,
            Problems: search.Problems
        );
    }

    private static PackageSourceVersions Describe(
        SourceSearchResult source,
        string packageId,
        bool includePrerelease,
        int take,
        PackageSourceMapping mapping
    )
    {
        string[] versions =
        [
            .. source
                .Versions.Where(version => includePrerelease || !NuGetVersionComparer.IsPrerelease(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderDescending(NuGetVersionComparer.Instance),
        ];

        return new(
            Source: source.Source,
            UsedByRestore: mapping.Allows(source: source.Source, packageId: packageId),
            VersionCount: versions.Length,
            Versions: [.. versions.Take(take)],
            LatestStable: Latest(versions: versions, prerelease: false),
            LatestPrerelease: Latest(versions: versions, prerelease: true)
        );
    }

    private static string? Latest(IEnumerable<string> versions, bool prerelease)
    {
        return versions
            .Where(version => NuGetVersionComparer.IsPrerelease(version) == prerelease)
            .Max(NuGetVersionComparer.Instance);
    }

    private static ToolUnavailableException NotFound(string packageId, PackageSearchResult search)
    {
        string[] failed =
        [
            .. search
                .Problems.Select(static problem => problem.Source)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        string searched = string.Join(separator: ", ", values: search.Sources.Select(static source => source.Source));

        return new(
            "No configured package source lists "
                + packageId
                + " (searched: "
                + (searched.Length == 0 ? "no sources" : searched)
                + ")"
                + (
                    failed.Length == 0
                        ? string.Empty
                        : "; these sources could not be searched: " + string.Join(separator: ", ", values: failed)
                )
        );
    }
}
