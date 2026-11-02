using System.Text.Json.Serialization;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
