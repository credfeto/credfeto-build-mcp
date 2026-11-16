using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Json;
using Credfeto.Build.Dotnet.Server.Models;
using Credfeto.Build.Dotnet.Server.Packages;
using Credfeto.Build.Server.Commands;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed partial class PackageVersionsTool : BuildTool
{
    public const string PACKAGE_ID_PATTERN = "^[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}$";
    public const int DEFAULT_TAKE = 20;
    public const int MAX_TAKE = 500;

    public static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan ConfigTimeout = TimeSpan.FromSeconds(30);

    private static readonly char[] LineBreaks = ['\r', '\n'];

    private static readonly SchemaProperty PackageId = new(
        Name: "package_id",
        Description: "The NuGet package id, as in a PackageReference, a tool manifest or an msbuild-sdks entry in global.json",
        Values: null,
        Pattern: PACKAGE_ID_PATTERN,
        DefaultValue: null,
        Required: true
    );

    private static readonly SchemaProperty IncludePrerelease = new(
        Name: "include_prerelease",
        Description: "Whether prerelease versions are listed too; leave it false unless a prerelease is explicitly needed",
        Values: null,
        Pattern: null,
        DefaultValue: SchemaProperty.FALSE,
        Required: false
    )
    {
        Flag = true,
    };

    private static readonly SchemaProperty Take = new(
        Name: "take",
        Description: "How many of the newest versions to list for each source",
        Values: null,
        Pattern: null,
        DefaultValue: DEFAULT_TAKE.ToString(CultureInfo.InvariantCulture),
        Required: false
    )
    {
        WholeNumber = true,
        Minimum = 1,
        Maximum = MAX_TAKE,
    };

    private readonly ToolContext _context;

    public PackageVersionsTool(ToolContext context)
        : base(
            name: ToolNames.PACKAGE_VERSIONS,
            title: "Package versions",
            description: "Lists the versions of a NuGet package on the repository's configured package sources (dotnet package search --exact-match from the solution directory, never nuget.org directly), newest first, with the latest stable and prerelease version for each source and overall. Each source says whether restore would use it for this id under the repository's package source mapping; the overall latest versions come only from those sources. Use this rather than querying nuget.org, which misses private feeds and ignores the mapping. Answers at once, no job",
            schema: [PackageId, IncludePrerelease, Take],
            readOnly: true
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        DotnetRepository repository = await this._context.CurrentRepositoryAsync(cancellationToken);
        string packageId = arguments.GetMatching(property: PackageId, pattern: PackageIdRegex());
        bool includePrerelease = arguments.GetFlag(IncludePrerelease);
        int take = arguments.GetInteger(Take);

        CommandOutput search = await this.RunAsync(
            step: DotnetCommands.PackageSearch(
                repository: repository,
                packageId: packageId,
                includePrerelease: includePrerelease
            ),
            timeout: SearchTimeout,
            cancellationToken: cancellationToken
        );
        PackageSearchResult found = PackageSearchParser.Parse(json: search.StandardOutput, packageId: packageId);
        PackageSourceMapping mapping = await this.LoadMappingAsync(
            repository: repository,
            cancellationToken: cancellationToken
        );

        PackageVersionsResponse response = PackageVersionsReport.Create(
            packageId: packageId,
            includePrerelease: includePrerelease,
            take: take,
            search: found,
            mapping: mapping
        );

        return JsonSerializer.Serialize(
            value: response,
            jsonTypeInfo: DotnetJsonContext.Default.PackageVersionsResponse
        );
    }

    public static IReadOnlyList<string> ConfigFiles(string output)
    {
        return
        [
            .. output
                .Split(
                    separator: LineBreaks,
                    options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Where(Path.IsPathFullyQualified),
        ];
    }

    private async ValueTask<PackageSourceMapping> LoadMappingAsync(
        DotnetRepository repository,
        CancellationToken cancellationToken
    )
    {
        CommandOutput paths = await this.RunAsync(
            step: DotnetCommands.NuGetConfigPaths(repository),
            timeout: ConfigTimeout,
            cancellationToken: cancellationToken
        );

        return await PackageSourceMapping.LoadAsync(
            configFiles: ConfigFiles(paths.StandardOutput),
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<CommandOutput> RunAsync(
        ProcessStep step,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        CommandOutput output = await this._context.Commands.RunAsync(
            step: step,
            timeout: timeout,
            cancellationToken: cancellationToken
        );

        if (output.TimedOut)
        {
            throw new ToolUnavailableException(
                "dotnet "
                    + step.Name
                    + " did not finish within "
                    + ((int)timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                    + " seconds"
            );
        }

        if (output.ExitCode == CommandOutput.NOT_STARTED)
        {
            throw new ToolUnavailableException("dotnet " + step.Name + " could not be started");
        }

        return output.Succeeded
            ? output
            : throw new ToolUnavailableException(
                "dotnet "
                    + step.Name
                    + " failed with exit code "
                    + output.ExitCode.ToString(CultureInfo.InvariantCulture)
            );
    }

    [GeneratedRegex(
        pattern: PACKAGE_ID_PATTERN,
        options: RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex PackageIdRegex();
}
