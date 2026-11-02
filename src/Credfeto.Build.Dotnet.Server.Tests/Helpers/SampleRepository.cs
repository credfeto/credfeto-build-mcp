using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

/// <summary>Writes a small .NET repository layout: a solution under src/ and one project of each kind.</summary>
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

    private const string PROJECT = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>";

    private const string SUPPORT_PROJECT = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <IsTestProject> false </IsTestProject>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>Adds a project to the solution and writes its project file, as dotnet new and dotnet sln add would.</summary>
    public static async ValueTask AddProjectAsync(TemporaryDirectory repository, string name, CancellationToken cancellationToken)
    {
        _ = await repository.WriteAsync(relative: "src/" + name + "/" + name + ".csproj", content: PROJECT, cancellationToken: cancellationToken);

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

    public static async ValueTask WriteAsync(TemporaryDirectory repository, CancellationToken cancellationToken)
    {
        _ = await repository.WriteAsync(relative: "src/Example.slnx", content: SLNX, cancellationToken: cancellationToken);

        foreach (string name in new[] { "Example.Core", "Example.Core.Tests", "Example.Api.Tests", "Example.Integration.Tests", "Example.Benchmark.Tests", "Example.Tests.Support" })
        {
            _ = await repository.WriteAsync(relative: "src/" + name + "/" + name + ".csproj", content: PROJECT, cancellationToken: cancellationToken);
        }

        _ = await repository.WriteAsync(
            relative: "src/Example.Mocks.Tests/Example.Mocks.Tests.csproj",
            content: SUPPORT_PROJECT,
            cancellationToken: cancellationToken
        );
    }
}
