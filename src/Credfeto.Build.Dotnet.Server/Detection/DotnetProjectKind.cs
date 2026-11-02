using System.Text.Json.Serialization;

namespace Credfeto.Build.Dotnet.Server.Detection;

public enum DotnetProjectKind
{
    [JsonStringEnumMemberName("project")]
    Project,

    [JsonStringEnumMemberName("unit-tests")]
    UnitTests,

    [JsonStringEnumMemberName("integration-tests")]
    IntegrationTests,

    [JsonStringEnumMemberName("benchmark-tests")]
    BenchmarkTests,
}
