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
    public const string PROGRESS = "notifications/progress";

    private readonly StreamReader _fromServer;
    private readonly List<string> _progress = [];
    private readonly Dictionary<int, string> _responses = [];
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

    // Returns once a tools/list_changed notification has arrived that this method has not yet returned for
    public async ValueTask WaitForListChangedAsync(CancellationToken cancellationToken)
    {
        while (this._unseenListChanges == 0)
        {
            await this.ReceiveNextAsync(cancellationToken);
        }

        this._unseenListChanges--;
    }

    // The params of every notifications/progress received so far, as raw JSON
    public IReadOnlyList<string> ProgressReceived => this._progress;

    // Sends a tools/call without waiting for its answer, so other requests can go out while it is open
    public async ValueTask<int> CallToolAsync(
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

    // The text of a tools/call answer
    public async ValueTask<string> ReceiveToolTextAsync(int id, CancellationToken cancellationToken)
    {
        using JsonDocument answer = await this.ReceiveResponseAsync(id: id, cancellationToken: cancellationToken);

        return answer.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()
            ?? string.Empty;
    }

    // True when the answer to the request has arrived and not yet been taken
    public bool HasResponse(int id)
    {
        return this._responses.ContainsKey(id);
    }

    public async ValueTask WaitForProgressAsync(int count, CancellationToken cancellationToken)
    {
        while (this._progress.Count < count)
        {
            await this.ReceiveNextAsync(cancellationToken);
        }
    }

    private async ValueTask<JsonDocument> ReceiveResponseAsync(int id, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (this._responses.Remove(key: id, value: out string? response))
            {
                return JsonDocument.Parse(response);
            }

            await this.ReceiveNextAsync(cancellationToken);
        }
    }

    // Reads one message: answers are kept until asked for, notifications are counted or kept on the way past
    private async ValueTask ReceiveNextAsync(CancellationToken cancellationToken)
    {
        string line = await this._fromServer.ReadLineAsync(cancellationToken)
            ?? throw new EndOfStreamException("The server closed its output");
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty(propertyName: "method", value: out JsonElement method))
        {
            string? name = method.GetString();

            if (StringComparer.Ordinal.Equals(x: name, y: TOOLS_LIST_CHANGED))
            {
                this._unseenListChanges++;
            }
            else if (StringComparer.Ordinal.Equals(x: name, y: PROGRESS))
            {
                this._progress.Add(root.GetProperty("params").GetRawText());
            }

            return;
        }

        if (
            root.TryGetProperty(propertyName: "id", value: out JsonElement responseId)
            && responseId.ValueKind == JsonValueKind.Number
        )
        {
            this._responses[responseId.GetInt32()] = line;
        }
    }
}
