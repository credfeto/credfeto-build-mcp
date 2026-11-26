using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;
using FunFair.Test.Common;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Build.Server.Tests.Tools;

public sealed partial class BuildToolTests : TestBase
{
    private const string JOB_ID = "0123456789abcdef0123456789abcdef";
    private const string PACKAGE_PATTERN = "^[A-Za-z]+$";

    private static readonly SchemaProperty Scope = new(
        Name: "scope",
        Description: "What to run",
        Values: ["all", "unit"],
        Pattern: null,
        DefaultValue: "all",
        Required: false
    );

    private static readonly SchemaProperty Verbose = new(
        Name: "verbose",
        Description: "Whether to say more",
        Values: null,
        Pattern: null,
        DefaultValue: SchemaProperty.FALSE,
        Required: false
    )
    {
        Flag = true,
    };

    private static readonly SchemaProperty Count = new(
        Name: "count",
        Description: "How many",
        Values: null,
        Pattern: null,
        DefaultValue: "5",
        Required: false
    )
    {
        WholeNumber = true,
        Minimum = 1,
        Maximum = 10,
    };

    private static readonly SchemaProperty Package = new(
        Name: "package_id",
        Description: "A package id",
        Values: null,
        Pattern: PACKAGE_PATTERN,
        DefaultValue: null,
        Required: false
    );

    private static readonly SchemaProperty Names = new(
        Name: "names",
        Description: "Some names",
        Values: null,
        Pattern: PACKAGE_PATTERN,
        DefaultValue: null,
        Required: false
    )
    {
        StringArray = true,
    };

    private static readonly SchemaProperty Id = new(
        Name: "id",
        Description: "A job id",
        Values: null,
        Pattern: ToolArguments.JOB_ID_PATTERN,
        DefaultValue: null,
        Required: false
    );

    [Fact]
    public async Task DefaultsApplyWhenNothingIsPassedAsync()
    {
        string answer = await this.CallAsync(arguments: Arguments.None(), execute: static arguments =>
            arguments.GetChoice(Scope)
            + "|"
            + arguments.GetFlag(Verbose).ToString(CultureInfo.InvariantCulture)
            + "|"
            + arguments.GetInteger(Count).ToString(CultureInfo.InvariantCulture)
        );

        Assert.Equal(expected: "all|False|5", actual: answer);
    }

    [Theory]
    [InlineData("scope", "unit")]
    [InlineData("package_id", "Example")]
    [InlineData("id", JOB_ID)]
    public async Task PassedTextIsReturnedAsync(string name, string value)
    {
        string answer = await this.CallAsync(arguments: Arguments.Text(name: name, value: value), execute: arguments =>
            Read(arguments: arguments, name: name)
        );

        Assert.Equal(expected: value, actual: answer);
    }

    [Fact]
    public async Task PassedFlagsAndNumbersAreReturnedAsync()
    {
        Assert.Equal(
            expected: "True",
            actual: await this.CallAsync(
                arguments: Arguments.Flag(name: "verbose", value: true),
                execute: static arguments => Read(arguments: arguments, name: "verbose")
            )
        );
        Assert.Equal(
            expected: "7",
            actual: await this.CallAsync(
                arguments: Arguments.Number(name: "count", value: 7),
                execute: static arguments => Read(arguments: arguments, name: "count")
            )
        );
    }

    [Theory]
    [InlineData("scope", "everything", "Invalid value for scope; expected one of: all, unit")]
    [InlineData("verbose", "true", "Invalid value for verbose; expected true or false")]
    [InlineData("count", "7", "Invalid value for count; expected a whole number from 1 to 10")]
    [InlineData(
        "package_id",
        "Example\n",
        "Invalid value for package_id; expected a value matching " + PACKAGE_PATTERN
    )]
    [InlineData("id", JOB_ID + "\n", "Invalid value for id; expected a job id from a previous call")]
    [InlineData("unknown", "x", "Unknown parameter; this tool takes: scope, verbose, count, package_id, names, id")]
    public async Task InvalidArgumentsAreToolErrorsWithFixedTextAsync(string name, string value, string message)
    {
        CallToolResult result = await this.ResultAsync(
            arguments: Arguments.Text(name: name, value: value),
            execute: arguments => Read(arguments: arguments, name: name)
        );

        Assert.True(condition: result.IsError, userMessage: name + " accepted " + value);
        Assert.Equal(expected: message, actual: Arguments.Answer(result));
    }

    [Fact]
    public async Task AnAbsentListIsEmptyAsync()
    {
        string answer = await this.CallAsync(arguments: Arguments.None(), execute: static arguments =>
            Read(arguments: arguments, name: "names")
        );

        Assert.Equal(expected: string.Empty, actual: answer);
    }

    [Fact]
    public async Task PassedListsAreReturnedInOrderAsync()
    {
        string answer = await this.CallAsync(
            arguments: Arguments.Strings("names", "Beta", "Alpha"),
            execute: static arguments => Read(arguments: arguments, name: "names")
        );

        Assert.Equal(expected: "Beta,Alpha", actual: answer);
    }

    [Theory]
    [InlineData("\"Alpha\"")]
    [InlineData("[1]")]
    [InlineData("[\"Alpha\", null]")]
    [InlineData("[\"Alpha\", \"Beta\\n\"]")]
    [InlineData("[\"\"]")]
    public async Task AListThatIsNotAllMatchingStringsIsRefusedAsync(string json)
    {
        CallToolResult result = await this.ResultAsync(
            arguments: Arguments.Json(name: "names", json: json),
            execute: static arguments => Read(arguments: arguments, name: "names")
        );

        Assert.True(condition: result.IsError, userMessage: "names accepted " + json);
        Assert.Equal(
            expected: "Invalid value for names; expected a list of values matching " + PACKAGE_PATTERN,
            actual: Arguments.Answer(result)
        );
    }

    [Fact]
    public async Task AnOutOfRangeNumberIsRefusedAsync()
    {
        CallToolResult result = await this.ResultAsync(
            arguments: Arguments.Number(name: "count", value: 11),
            execute: static arguments => Read(arguments: arguments, name: "count")
        );

        Assert.True(condition: result.IsError, userMessage: "11 is out of range");
    }

    [Fact]
    public async Task ANumberIsNotAStringAsync()
    {
        CallToolResult result = await this.ResultAsync(
            arguments: Arguments.Number(name: "scope", value: 1),
            execute: static arguments => Read(arguments: arguments, name: "scope")
        );

        Assert.Equal(expected: "Invalid value for scope; expected a string", actual: Arguments.Answer(result));
    }

    [Fact]
    public async Task AMissingRequiredParameterIsRefusedAsync()
    {
        EchoTool tool = new(schema: [Scope with { Required = true }], execute: static _ => "ran");

        CallToolResult result = await tool.CallAsync(
            arguments: Arguments.None(),
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result.IsError, userMessage: "scope is required");
        Assert.Equal(expected: "Missing required parameter scope", actual: Arguments.Answer(result));
    }

    [Fact]
    public async Task AChoiceFromAnEmptyListIsRefusedAsync()
    {
        CallToolResult result = await this.ResultAsync(
            arguments: Arguments.Text(name: "scope", value: "unit"),
            execute: static arguments => arguments.GetChoice(property: Scope, allowed: []) ?? "none"
        );

        Assert.Equal(
            expected: "Invalid value for scope; there is nothing to choose from now",
            actual: Arguments.Answer(result)
        );
    }

    [Fact]
    public async Task AToolThatIsUnavailableReportsItsFixedMessageAsync()
    {
        CallToolResult result = await this.ResultAsync(arguments: Arguments.None(), execute: static _ =>
            throw new ToolUnavailableException("Not in this repository")
        );

        Assert.True(condition: result.IsError, userMessage: "unavailable is an error");
        Assert.Equal(expected: "Not in this repository", actual: Arguments.Answer(result));
    }

    [Fact]
    public static void TheProtocolToolDescribesTheToolAndItsSchema()
    {
        EchoTool tool = new(schema: [Scope], execute: static _ => "ran");

        Assert.Equal(expected: "echo", actual: tool.ProtocolTool.Name);
        Assert.Equal(expected: "Echo", actual: tool.ProtocolTool.Annotations?.Title);
        Assert.False(
            condition: tool.ProtocolTool.Annotations?.ReadOnlyHint,
            userMessage: "the echo tool is not read-only"
        );
        Assert.Equal(expected: "object", actual: tool.ProtocolTool.InputSchema.GetProperty("type").GetString());
        Assert.Empty(tool.Metadata);
    }

    private async Task<string> CallAsync(Dictionary<string, JsonElement> arguments, Func<ToolArguments, string> execute)
    {
        CallToolResult result = await this.ResultAsync(arguments: arguments, execute: execute);

        Assert.False(condition: result.IsError, userMessage: Arguments.Answer(result));

        return Arguments.Answer(result);
    }

    private ValueTask<CallToolResult> ResultAsync(
        Dictionary<string, JsonElement> arguments,
        Func<ToolArguments, string> execute
    )
    {
        EchoTool tool = new(schema: [Scope, Verbose, Count, Package, Names, Id], execute: execute);

        return tool.CallAsync(arguments: arguments, cancellationToken: this.CancellationToken());
    }

    private static string Read(ToolArguments arguments, string name)
    {
        return name switch
        {
            "scope" => arguments.GetChoice(Scope) ?? "none",
            "verbose" => arguments.GetFlag(Verbose).ToString(CultureInfo.InvariantCulture),
            "count" => arguments.GetInteger(Count).ToString(CultureInfo.InvariantCulture),
            "package_id" => arguments.GetMatching(property: Package, pattern: PackageId()),
            "id" => arguments.GetJobId(Id),
            "names" => string.Join(separator: ',', values: arguments.GetStrings(property: Names, pattern: PackageId())),
            _ => "ran",
        };
    }

    [GeneratedRegex(pattern: PACKAGE_PATTERN, options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PackageId();
}
