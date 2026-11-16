using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Testing;

// A client session that stays open, so a test can watch for notifications between requests
public sealed class McpSession
{
    public const string TOOLS_LIST_CHANGED = "notifications/tools/list_changed";

    private const string PROGRESS = "notifications/progress";

    private readonly StreamReader _fromServer;
    private readonly Stream _toServer;
    private int _nextId = 10;
    private int _unseenListChanges;

    public McpSession(Stream toServer, StreamReader fromServer)
    {
        this._toServer = toServer;
        this._fromServer = fromServer;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await McpProbe.SendAsync(
            toServer: this._toServer,
            message: McpProbe.INITIALIZE,
            cancellationToken: cancellationToken
        );
        using JsonDocument initialized = await this.ReceiveResponseAsync(id: 1, cancellationToken: cancellationToken);
        await McpProbe.SendAsync(
            toServer: this._toServer,
            message: McpProbe.INITIALIZED,
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken)
    {
        int id = ++this._nextId;
        await McpProbe.SendAsync(
            toServer: this._toServer,
            message: """{"jsonrpc":"2.0","id":"""
                + id.ToString(CultureInfo.InvariantCulture)
                + ""","method":"tools/list","params":{}}""",
            cancellationToken: cancellationToken
        );

        using JsonDocument listed = await this.ReceiveResponseAsync(id: id, cancellationToken: cancellationToken);

        return
        [
            .. listed
                .RootElement.GetProperty("result")
                .GetProperty("tools")
                .EnumerateArray()
                .Select(static tool => tool.GetProperty("name").GetString() ?? string.Empty),
        ];
    }

    public int ProgressNotifications { get; private set; }

    // Sends a tools/call without waiting for its answer, so the test can make other calls while it runs; returns
    // the request id to hand to ReceiveToolAnswerAsync
    public async ValueTask<int> SendToolCallAsync(
        string name,
        string argumentsJson,
        string? progressToken,
        CancellationToken cancellationToken
    )
    {
        int id = ++this._nextId;
        string meta = progressToken is null
            ? string.Empty
            : ""","_meta":{"progressToken":"""
                + JsonSerializer.Serialize(value: progressToken, jsonTypeInfo: TestJsonContext.Default.String)
                + "}";

        await McpProbe.SendAsync(
            toServer: this._toServer,
            message: """{"jsonrpc":"2.0","id":"""
                + id.ToString(CultureInfo.InvariantCulture)
                + ""","method":"tools/call","params":{"name":"""
                + JsonSerializer.Serialize(value: name, jsonTypeInfo: TestJsonContext.Default.String)
                + ""","arguments":"""
                + argumentsJson
                + meta
                + "}}",
            cancellationToken: cancellationToken
        );

        return id;
    }

    // The text of the tool's answer to the call with this id; messages for other calls read on the way are dropped
    public async ValueTask<string> ReceiveToolAnswerAsync(int id, CancellationToken cancellationToken)
    {
        using JsonDocument answer = await this.ReceiveResponseAsync(id: id, cancellationToken: cancellationToken);

        return answer.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()
            ?? string.Empty;
    }

    // Returns once a tools/list_changed notification has arrived that this method has not yet returned for
    public async ValueTask WaitForListChangedAsync(CancellationToken cancellationToken)
    {
        while (this._unseenListChanges == 0)
        {
            using JsonDocument message = await this.ReceiveAsync(cancellationToken);
        }

        this._unseenListChanges--;
    }

    private async ValueTask<JsonDocument> ReceiveResponseAsync(int id, CancellationToken cancellationToken)
    {
        while (true)
        {
            JsonDocument document = await this.ReceiveAsync(cancellationToken);

            if (
                document.RootElement.TryGetProperty(propertyName: "id", value: out JsonElement responseId)
                && responseId.ValueKind == JsonValueKind.Number
                && responseId.GetInt32() == id
            )
            {
                return document;
            }

            document.Dispose();
        }
    }

    // Reads one message, counting every tools/list_changed and progress notification on the way past
    private async ValueTask<JsonDocument> ReceiveAsync(CancellationToken cancellationToken)
    {
        string line = await this._fromServer.ReadLineAsync(cancellationToken)
            ?? throw new EndOfStreamException("The server closed its output");
        JsonDocument document = JsonDocument.Parse(line);

        if (document.RootElement.TryGetProperty(propertyName: "method", value: out JsonElement method))
        {
            string? name = method.GetString();

            if (StringComparer.Ordinal.Equals(x: name, y: TOOLS_LIST_CHANGED))
            {
                this._unseenListChanges++;
            }
            else if (StringComparer.Ordinal.Equals(x: name, y: PROGRESS))
            {
                this.ProgressNotifications++;
            }
        }

        return document;
    }
}
