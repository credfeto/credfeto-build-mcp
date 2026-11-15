using System.Text.Json.Serialization;

namespace Credfeto.Build.Dotnet.Server.Tests.Helpers;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
