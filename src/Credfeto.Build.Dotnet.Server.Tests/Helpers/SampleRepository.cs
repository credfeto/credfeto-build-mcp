using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

internal static class SampleRepository
{
    public const string SLNX = """
        <Solution>
          <Folder Name="/Config/">
            <File Path="global.json" />
          </Folder>
          <Project Path="Example.Core/Example.Core.csproj" />
          <Project Path="Example.Core.Tests/Example.Core.Tests.csproj" />
          <Project Path="Example.Api.Tests/Example.Api.Tests.csproj" />
          <Project Path="Example.Integration.Tests/Example.Integration.Tests.csproj" />
          <Project Path="Example.Benchmark.Tests/Example.Benchmark.Tests.csproj" />
          <Project Path="Example.Tests.Support/Example.Tests.Support.csproj" />
          <Project Path="Example.Mocks.Tests/Example.Mocks.Tests.csproj" />
          <Project Path="Example.Missing.Tests/Example.Missing.Tests.csproj" />
        </Solution>
        """;

    public const string SLN = """
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Example.Core", "Example.Core\Example.Core.csproj", "{11111111-1111-1111-1111-111111111111}"
        EndProject
        Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Example.Core.Tests", "Example.Core.Tests\Example.Core.Tests.csproj", "{22222222-2222-2222-2222-222222222222}"
        EndProject
        """;

    private const string PROJECT = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>";

    private const string SUPPORT_PROJECT = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <IsTestProject> false </IsTestProject>
          </PropertyGroup>
        </Project>
        """;

    public static async ValueTask AddProjectAsync(
        TemporaryDirectory repository,
        string name,
        CancellationToken cancellationToken
    )
    {
        _ = await repository.WriteAsync(
            relative: "src/" + name + "/" + name + ".csproj",
            content: PROJECT,
            cancellationToken: cancellationToken
        );

        string solution = repository.Combine("src/Example.slnx");
        string content = await File.ReadAllTextAsync(path: solution, cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(
            path: solution,
            contents: content.Replace(
                oldValue: "</Solution>",
                newValue: "  <Project Path=\"" + name + "/" + name + ".csproj\" />\n</Solution>",
                comparisonType: StringComparison.Ordinal
            ),
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask WriteSlnAsync(TemporaryDirectory repository, CancellationToken cancellationToken)
    {
        foreach (string name in new[] { "Example.Core", "Example.Core.Tests" })
        {
            _ = await repository.WriteAsync(
                relative: "src/" + name + "/" + name + ".csproj",
                content: PROJECT,
                cancellationToken: cancellationToken
            );
        }

        _ = await repository.WriteAsync(
            relative: "src/Example.sln",
            content: SLN,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask WriteAsync(TemporaryDirectory repository, CancellationToken cancellationToken)
    {
        _ = await repository.WriteAsync(
            relative: "src/Example.slnx",
            content: SLNX,
            cancellationToken: cancellationToken
        );

        foreach (
            string name in new[]
            {
                "Example.Core",
                "Example.Core.Tests",
                "Example.Api.Tests",
                "Example.Integration.Tests",
                "Example.Benchmark.Tests",
                "Example.Tests.Support",
            }
        )
        {
            _ = await repository.WriteAsync(
                relative: "src/" + name + "/" + name + ".csproj",
                content: PROJECT,
                cancellationToken: cancellationToken
            );
        }

        _ = await repository.WriteAsync(
            relative: "src/Example.Mocks.Tests/Example.Mocks.Tests.csproj",
            content: SUPPORT_PROJECT,
            cancellationToken: cancellationToken
        );
    }
}
