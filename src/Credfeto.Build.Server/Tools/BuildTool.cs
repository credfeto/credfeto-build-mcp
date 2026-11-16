using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs.Exceptions;
using Credfeto.Build.Server.Tools.Exceptions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Tools;

// A tool with a hand-built schema, so a schema can carry enums that are only known once the repository has been
// inspected, with no reflection-based tool binding
public abstract class BuildTool : McpServerTool
{
    protected BuildTool(
        string name,
        string title,
        string description,
        IReadOnlyList<SchemaProperty> schema,
        bool readOnly
    )
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
        return this.CallAsync(
            arguments: request.Params?.Arguments,
            progress: ToolProgress.For(request: request, error: Console.Error),
            cancellationToken: cancellationToken
        );
    }

    public ValueTask<CallToolResult> CallAsync(
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments,
        CancellationToken cancellationToken
    )
    {
        return this.CallAsync(arguments: arguments, progress: ToolProgress.None, cancellationToken: cancellationToken);
    }

    public async ValueTask<CallToolResult> CallAsync(
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        try
        {
            ToolArguments parsed = new(arguments: arguments, schema: this.Schema);
            string json = await this.ExecuteAsync(
                arguments: parsed,
                progress: progress,
                cancellationToken: cancellationToken
            );

            return Result(text: json, isError: false);
        }
        catch (Exception exception)
            when (exception
                    is ToolArgumentException
                        or ToolUnavailableException
                        or JobNotFoundException
                        or JobAlreadyRunningException
            )
        {
            // These messages are fixed text, never caller input or process output
            return Result(text: exception.Message, isError: true);
        }
    }

    protected abstract ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken);

    // A tool that reports progress while it works overrides this; the others never see the reporter
    protected virtual ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        return this.ExecuteAsync(arguments: arguments, cancellationToken: cancellationToken);
    }

    private static CallToolResult Result(string text, bool isError)
    {
        return new() { Content = [new TextContentBlock { Text = text }], IsError = isError };
    }
}
