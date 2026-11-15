using System.Text.Json;
using Credfeto.Build.Server.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Server.Tests.Tools;

public sealed class ToolSchemaTests : TestBase
{
    [Fact]
    public static void NoPropertiesIsAClosedEmptyObject()
    {
        JsonElement schema = ToolSchema.Create([]);

        Assert.Equal(
            expected: """{"type":"object","properties":{},"additionalProperties":false}""",
            actual: schema.GetRawText()
        );
    }

    [Fact]
    public static void EveryKindOfPropertyIsWritten()
    {
        JsonElement schema = ToolSchema.Create([
            new(
                Name: "scope",
                Description: "S",
                Values: ["all", "unit"],
                Pattern: null,
                DefaultValue: "all",
                Required: false
            ),
            new(Name: "id", Description: "I", Values: null, Pattern: "^[0-9]*$", DefaultValue: null, Required: true),
            new(
                Name: "write",
                Description: "W",
                Values: null,
                Pattern: null,
                DefaultValue: SchemaProperty.TRUE,
                Required: false
            )
            {
                Flag = true,
            },
            new(Name: "count", Description: "C", Values: null, Pattern: null, DefaultValue: "3", Required: false)
            {
                WholeNumber = true,
                Minimum = 1,
                Maximum = 9,
            },
        ]);

        Assert.Equal(
            expected: """{"type":"object","properties":{"scope":{"type":"string","description":"S","enum":["all","unit"],"default":"all"},"id":{"type":"string","description":"I","pattern":"^[0-9]*$"},"write":{"type":"boolean","description":"W","default":true},"count":{"type":"integer","description":"C","minimum":1,"maximum":9,"default":3}},"required":["id"],"additionalProperties":false}""",
            actual: schema.GetRawText()
        );
    }
}
