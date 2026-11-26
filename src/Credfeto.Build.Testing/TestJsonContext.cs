using System.Text.Json.Serialization;

namespace Credfeto.Build.Testing;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string[]))]
public sealed partial class TestJsonContext : JsonSerializerContext;
