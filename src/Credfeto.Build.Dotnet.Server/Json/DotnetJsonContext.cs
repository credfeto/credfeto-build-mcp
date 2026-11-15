using System.Text.Json.Serialization;
using Credfeto.Build.Dotnet.Server.Models;

namespace Credfeto.Build.Dotnet.Server.Json;

[JsonSerializable(typeof(DetectResponse))]
[JsonSerializable(typeof(PackageVersionsResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public sealed partial class DotnetJsonContext : JsonSerializerContext;
