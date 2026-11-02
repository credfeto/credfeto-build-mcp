using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Dotnet.Server.Tests.Protocol;

/// <summary>A minimal MCP client: initialize, initialized, then tools/list, as newline-delimited JSON-RPC.</summary>
internal static class McpProbe
{
    public const string INITIALIZE =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}""";

    public const string INITIALIZED = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";

    private const string LIST_TOOLS = """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""";

    public static async ValueTask<McpProbeResult> ListToolsAsync(Stream toServer, StreamReader fromServer, CancellationToken cancellationToken)
    {
        await SendAsync(toServer: toServer, message: INITIALIZE, cancellationToken: cancellationToken);
        using JsonDocument initialized = await ReceiveAsync(fromServer: fromServer, id: 1, cancellationToken: cancellationToken);

        await SendAsync(toServer: toServer, message: INITIALIZED, cancellationToken: cancellationToken);
        await SendAsync(toServer: toServer, message: LIST_TOOLS, cancellationToken: cancellationToken);
        using JsonDocument listed = await ReceiveAsync(fromServer: fromServer, id: 2, cancellationToken: cancellationToken);

        JsonElement initializeResult = initialized.RootElement.GetProperty("result");
        string[] names =
        [
            .. listed
                .RootElement.GetProperty("result")
                .GetProperty("tools")
                .EnumerateArray()
                .Select(static tool => tool.GetProperty("name").GetString() ?? string.Empty),
        ];

        bool advertisesTools = initializeResult.GetProperty("capabilities").TryGetProperty(propertyName: "tools", value: out JsonElement tools);

        return new(
            ToolNames: names,
            AdvertisesTools: advertisesTools,
            AdvertisesListChanged: advertisesTools
                && tools.TryGetProperty(propertyName: "listChanged", value: out JsonElement listChanged)
                && listChanged.ValueKind == JsonValueKind.True,
            Instructions: initializeResult.TryGetProperty(propertyName: "instructions", value: out JsonElement instructions)
                ? instructions.GetString()
                : null
        );
    }

    public static async ValueTask SendAsync(Stream toServer, string message, CancellationToken cancellationToken)
    {
        await toServer.WriteAsync(buffer: Encoding.UTF8.GetBytes(message + "\n"), cancellationToken: cancellationToken);
        await toServer.FlushAsync(cancellationToken);
    }

    private static async ValueTask<JsonDocument> ReceiveAsync(StreamReader fromServer, int id, CancellationToken cancellationToken)
    {
        while (true)
        {
            string line = await fromServer.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException("The server closed its output");
            JsonDocument document = JsonDocument.Parse(line);

            if (document.RootElement.TryGetProperty(propertyName: "id", value: out JsonElement responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
            {
                return document;
            }

            document.Dispose();
        }
    }
}
