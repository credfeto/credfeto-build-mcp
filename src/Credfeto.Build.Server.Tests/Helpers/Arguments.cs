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

    public static string Answer(CallToolResult result)
    {
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }
}
