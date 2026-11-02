using System.Text.Json.Serialization;
using Credfeto.Build.Dotnet.Server.Models;

namespace Credfeto.Build.Dotnet.Server.Json;

[JsonSerializable(typeof(DetectResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public sealed partial class DotnetJsonContext : JsonSerializerContext;
