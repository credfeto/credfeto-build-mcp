using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace Credfeto.Build.Server.Workspace;

// Owns the live tool collection: a refresh whose detection changes the signature swaps the whole tool list in one
// change, so the SDK sends a single notifications/tools/list_changed
public sealed class LanguageWorkspace<TDetection> : IDetectionSource<TDetection>, IRepositoryChangeHandler, IDisposable
    where TDetection : class
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);
    private readonly IWorkspaceLanguage<TDetection> _language;
    private TDetection _current;
    private string _signature;

    public LanguageWorkspace(TDetection initial, IWorkspaceLanguage<TDetection> language)
    {
        this._language = language;
        this._current = initial;
        this._signature = language.Signature(initial);
        this.Tools = [];
        this.AddAll(SortedTools(language, initial));
    }

    public McpServerPrimitiveCollection<McpServerTool> Tools { get; }

    public TDetection Current => Volatile.Read(ref this._current);

    public void Dispose()
    {
        this._gate.Dispose();
    }

    public async ValueTask<TDetection> RefreshAsync(CancellationToken cancellationToken)
    {
        await this._gate.WaitAsync(cancellationToken);

        try
        {
            TDetection fresh = await this._language.DetectAsync(cancellationToken);
            string signature = this._language.Signature(fresh);

            // Only one refresh runs at a time, so the signature needs no other lock
            if (StringComparer.Ordinal.Equals(x: signature, y: this._signature))
            {
                return fresh;
            }

            this._signature = signature;
            Volatile.Write(location: ref this._current, value: fresh);
            this.Replace(fresh);

            return fresh;
        }
        finally
        {
            this._gate.Release();
        }
    }

    public async ValueTask RepositoryChangedAsync(CancellationToken cancellationToken)
    {
        _ = await this.RefreshAsync(cancellationToken);
    }

    public bool IsTransientFailure(Exception exception)
    {
        return this._language.IsTransientFailure(exception);
    }

    private void Replace(TDetection detection)
    {
        McpServerTool[] tools = SortedTools(this._language, detection);

        using (this.Tools.DeferChangedEvents())
        {
            this.Tools.Clear();
            this.AddAll(tools);
        }
    }

    // The collection lists tools in the order they were added, so adding them by name keeps tools/list sorted
    private static McpServerTool[] SortedTools(IWorkspaceLanguage<TDetection> language, TDetection detection)
    {
        return
        [
            .. language
                .CreateTools(detection)
                .OrderBy(
                    keySelector: static tool => tool.ProtocolTool.Name,
                    comparer: StringComparer.OrdinalIgnoreCase
                ),
        ];
    }

    private void AddAll(McpServerTool[] tools)
    {
        foreach (McpServerTool tool in tools)
        {
            this.Tools.Add(tool);
        }
    }
}
