using System;
using System.Collections.Generic;
using System.Text.Json;
using Credfeto.Build.Testing;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Server.Tests.Helpers;

internal static class Arguments
{
    public static Dictionary<string, JsonElement> None()
    {
        return [with(StringComparer.Ordinal)];
    }

    public static Dictionary<string, JsonElement> Text(string name, string value)
    {
        return new(StringComparer.Ordinal)
        {
            [name] = JsonSerializer.SerializeToElement(value: value, jsonTypeInfo: TestJsonContext.Default.String),
        };
    }

    public static Dictionary<string, JsonElement> Number(string name, int value)
    {
        return new(StringComparer.Ordinal)
        {
            [name] = JsonSerializer.SerializeToElement(value: value, jsonTypeInfo: TestJsonContext.Default.Int32),
        };
    }

    public static Dictionary<string, JsonElement> Flag(string name, bool value)
    {
        return new(StringComparer.Ordinal)
        {
            [name] = JsonSerializer.SerializeToElement(value: value, jsonTypeInfo: TestJsonContext.Default.Boolean),
        };
    }

    // The job id plus a wait_seconds given as raw JSON, so a test can send a value of the wrong type
    public static Dictionary<string, JsonElement> JobWait(string id, string waitJson)
    {
        Dictionary<string, JsonElement> arguments = Text(name: "id", value: id);

        using JsonDocument wait = JsonDocument.Parse(waitJson);
        arguments["wait_seconds"] = wait.RootElement.Clone();

        return arguments;
    }

    public static string Answer(CallToolResult result)
    {
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }
}
