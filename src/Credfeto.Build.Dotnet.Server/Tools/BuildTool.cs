using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs.Exceptions;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>
///     A tool with a hand-built schema, so a schema can carry enums that are only known once the repository has
///     been inspected, with no reflection-based tool binding.
/// </summary>
public abstract class BuildTool : McpServerTool
{
    protected BuildTool(string name, string title, string description, IReadOnlyList<SchemaProperty> schema, bool readOnly)
    {
        this.Schema = schema;
        this.ProtocolTool = new()
        {
            Name = name,
            Title = title,
            Description = description,
            InputSchema = ToolSchema.Create(schema),
            Annotations = new()
            {
                Title = title,
                ReadOnlyHint = readOnly,
                DestructiveHint = false,
                IdempotentHint = readOnly,
                OpenWorldHint = false,
            },
        };
    }

    public IReadOnlyList<SchemaProperty> Schema { get; }

    public override Tool ProtocolTool { get; }

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default
    )
    {
        return this.CallAsync(arguments: request.Params?.Arguments, cancellationToken: cancellationToken);
    }

    public async ValueTask<CallToolResult> CallAsync(
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments,
        CancellationToken cancellationToken
    )
    {
        try
        {
            ToolArguments parsed = new(arguments: arguments, schema: this.Schema);
            string json = await this.ExecuteAsync(arguments: parsed, cancellationToken: cancellationToken);

            return Result(text: json, isError: false);
        }
        catch (Exception exception)
            when (exception is ToolArgumentException or JobNotFoundException or JobAlreadyRunningException)
        {
            // These messages are fixed text, never caller input or process output
            return Result(text: exception.Message, isError: true);
        }
    }

    protected abstract ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken);

    private static CallToolResult Result(string text, bool isError)
    {
        return new() { Content = [new TextContentBlock { Text = text }], IsError = isError };
    }
}
