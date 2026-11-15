using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Server.Workspace;

// Watches the repository for the files the language's filter names (and .ai/commands.json) being created, deleted,
// renamed or edited, and for directories coming and going, and tells the handler once the changes settle. Anything
// under one of the filter's ignored directories is ignored.
public sealed class RepositoryWatcher : IAsyncDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(500);

    private const string OVERRIDES_FILE = "commands.json";
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly TimeSpan _debounce;
    private readonly WatchFilter _filter;
    private readonly Lock _lock = new();
    private readonly string _repositoryRoot;
    private readonly IRepositoryChangeHandler _source;
    private readonly CancellationTokenSource _stopping = new();
    private readonly TimeProvider _timeProvider;
    private bool _disposed;
    private Task _pending = Task.CompletedTask;
    private ITimer? _timer;
    private FileSystemWatcher? _watcher;

    public RepositoryWatcher(
        string repositoryRoot,
        IRepositoryChangeHandler source,
        WatchFilter filter,
        TimeProvider timeProvider,
        TimeSpan debounce
    )
    {
        this._repositoryRoot = repositoryRoot;
        this._source = source;
        this._filter = filter;
        this._timeProvider = timeProvider;
        this._debounce = debounce;
    }

    public bool IsWatching
    {
        get
        {
            lock (this._lock)
            {
                return this._watcher is not null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        FileSystemWatcher? watcher;
        ITimer? timer;

        lock (this._lock)
        {
            this._disposed = true;
            watcher = this._watcher;
            timer = this._timer;
            this._watcher = null;
            this._timer = null;
        }

        watcher?.Dispose();

        if (timer is not null)
        {
            await timer.DisposeAsync();
        }

        await this._stopping.CancelAsync();
        await this.WaitForPendingRefreshAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        this._stopping.Dispose();
    }

    private Task WaitForPendingRefreshAsync()
    {
        lock (this._lock)
        {
            return this._pending.WaitAsync(CancellationToken.None);
        }
    }

    // Starts watching. When the repository root does not exist, or the system refuses another watch, the server
    // carries on without one: every tool call still checks against a fresh detection
    public bool Start()
    {
        if (!Directory.Exists(this._repositoryRoot))
        {
            return false;
        }

        FileSystemWatcher watcher = new(this._repositoryRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
        };

        // No name filter: a directory created (or moved in) with projects already inside raises no event for
        // those files, so directory events count too. Everything else is dropped in the handlers
        watcher.Created += this.OnCreated;
        watcher.Deleted += this.OnDeleted;
        watcher.Changed += this.OnChanged;
        watcher.Renamed += this.OnRenamed;
        watcher.Error += this.OnError;

        try
        {
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or PlatformNotSupportedException
            )
        {
            watcher.Dispose();

            return false;
        }

        lock (this._lock)
        {
            if (this._disposed)
            {
                watcher.Dispose();

                return false;
            }

            this._watcher = watcher;
        }

        // Anything that changed between start-up detection and the watch starting
        this.Schedule();

        return true;
    }

    public bool IsIgnored(string fullPath)
    {
        string relative = Path.GetRelativePath(relativeTo: this._repositoryRoot, path: fullPath);

        return relative.Split(Separators).SkipLast(1).Any(this.IsIgnoredDirectory);
    }

    // Whether a change to this path can change what detection finds
    public bool IsRelevant(string fullPath, bool mayBeDirectory)
    {
        if (this.IsIgnored(fullPath))
        {
            return false;
        }

        string name = Path.GetFileName(fullPath);
        string extension = Path.GetExtension(name);

        return this._filter.Extensions.Contains(value: extension, comparer: StringComparer.OrdinalIgnoreCase)
            || StringComparer.OrdinalIgnoreCase.Equals(x: name, y: OVERRIDES_FILE)
            || mayBeDirectory && !this.IsIgnoredDirectory(name);
    }

    private bool IsIgnoredDirectory(string name)
    {
        return this._filter.IgnoredDirectories.Contains(value: name, comparer: StringComparer.Ordinal);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        this.ScheduleIf(this.IsRelevant(fullPath: e.FullPath, mayBeDirectory: Directory.Exists(e.FullPath)));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        // What was deleted can no longer be inspected; a name without an extension may have been a directory
        this.ScheduleIf(this.IsRelevant(fullPath: e.FullPath, mayBeDirectory: !Path.HasExtension(e.FullPath)));
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        this.ScheduleIf(this.IsRelevant(fullPath: e.FullPath, mayBeDirectory: false));
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        bool mayBeDirectory = Directory.Exists(e.FullPath);

        this.ScheduleIf(
            this.IsRelevant(fullPath: e.FullPath, mayBeDirectory: mayBeDirectory)
                || this.IsRelevant(fullPath: e.OldFullPath, mayBeDirectory: mayBeDirectory)
        );
    }

    private void ScheduleIf(bool relevant)
    {
        if (relevant)
        {
            this.Schedule();
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Events were lost (for example the kernel queue overflowed), so look again
        this.Schedule();
    }

    private void Schedule()
    {
        lock (this._lock)
        {
            if (this._disposed)
            {
                return;
            }

            if (this._timer is null)
            {
                this._timer = this._timeProvider.CreateTimer(
                    callback: this.OnSettled,
                    state: null,
                    dueTime: this._debounce,
                    period: Timeout.InfiniteTimeSpan
                );
            }
            else
            {
                _ = this._timer.Change(dueTime: this._debounce, period: Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnSettled(object? state)
    {
        lock (this._lock)
        {
            if (this._disposed)
            {
                return;
            }

            this._pending = this.RefreshAfterAsync(this._pending);
        }
    }

    private async Task RefreshAfterAsync(Task previous)
    {
        // Leave the caller's lock before any detection runs
        await Task.Yield();

        CancellationToken cancellationToken = this._stopping.Token;

        await previous.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        try
        {
            await this._source.RepositoryChangedAsync(cancellationToken);
        }
        catch (Exception exception)
            when (exception is OperationCanceledException or IOException or UnauthorizedAccessException
                || this._source.IsTransientFailure(exception)
            )
        {
            // Shutting down, or the repository is mid-change; the next event or tool call looks again
        }
    }
}
