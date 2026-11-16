using System.Text.Json.Serialization;
using Credfeto.Build.Node.Server.Models;

namespace Credfeto.Build.Node.Server.Json;

[JsonSerializable(typeof(DetectResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public sealed partial class NodeJsonContext : JsonSerializerContext;
