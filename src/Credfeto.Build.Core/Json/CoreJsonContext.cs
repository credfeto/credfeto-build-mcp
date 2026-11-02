using System.Text.Json;
using System.Text.Json.Serialization;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Core.Overrides;

namespace Credfeto.Build.Core.Json;

[JsonSerializable(typeof(RepositoryOverrides))]
[JsonSerializable(typeof(JobStatus))]
[JsonSerializable(typeof(JobResult))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
)]
public sealed partial class CoreJsonContext : JsonSerializerContext;
