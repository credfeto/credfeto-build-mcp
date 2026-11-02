using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Dotnet.Server.Tests.Protocol;

/// <summary>A client session that stays open, so a test can watch for notifications between requests.</summary>
internal sealed class McpSession
{
    public const string TOOLS_LIST_CHANGED = "notifications/tools/list_changed";

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
        await McpProbe.SendAsync(toServer: this._toServer, message: McpProbe.INITIALIZE, cancellationToken: cancellationToken);
        using JsonDocument initialized = await this.ReceiveResponseAsync(id: 1, cancellationToken: cancellationToken);
        await McpProbe.SendAsync(toServer: this._toServer, message: McpProbe.INITIALIZED, cancellationToken: cancellationToken);
    }

    public async ValueTask<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken)
    {
        int id = ++this._nextId;
        await McpProbe.SendAsync(
            toServer: this._toServer,
            message: """{"jsonrpc":"2.0","id":""" + id.ToString(CultureInfo.InvariantCulture) + ""","method":"tools/list","params":{}}""",
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

    /// <summary>Returns once a tools/list_changed notification has arrived that this method has not yet returned for.</summary>
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

    /// <summary>Reads one message, counting every tools/list_changed notification on the way past.</summary>
    private async ValueTask<JsonDocument> ReceiveAsync(CancellationToken cancellationToken)
    {
        string line = await this._fromServer.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException("The server closed its output");
        JsonDocument document = JsonDocument.Parse(line);

        if (
            document.RootElement.TryGetProperty(propertyName: "method", value: out JsonElement method)
            && StringComparer.Ordinal.Equals(x: method.GetString(), y: TOOLS_LIST_CHANGED)
        )
        {
            this._unseenListChanges++;
        }

        return document;
    }
}
