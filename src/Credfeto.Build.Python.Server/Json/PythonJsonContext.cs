using System.Text.Json.Serialization;
using Credfeto.Build.Python.Server.Models;

namespace Credfeto.Build.Python.Server.Json;

[JsonSerializable(typeof(DetectResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public sealed partial class PythonJsonContext : JsonSerializerContext;
