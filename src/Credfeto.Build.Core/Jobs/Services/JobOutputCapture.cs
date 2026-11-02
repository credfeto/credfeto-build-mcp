using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs.Services;

/// <summary>
///     Writes masked, length-capped output to the job's log file and keeps the last lines in memory.
/// </summary>
internal sealed partial class JobOutputCapture : IAsyncDisposable
{
    private const string TRUNCATED_LINE = " [line truncated]";
    private const string TRUNCATED_LOG = "[log truncated: the output limit was reached; later lines are not recorded]";

    private readonly SemaphoreSlim _fileLock = new(initialCount: 1, maxCount: 1);
    private readonly IOutputMasker _masker;
    private readonly ITestOutputParser? _parser;
    private readonly JobRunnerSettings _settings;
    private readonly Queue<string> _tail;
    private readonly Lock _tailLock = new();
    private readonly StreamWriter _writer;
    private bool _truncated;
    private long _written;

    public JobOutputCapture(string logPath, IOutputMasker masker, ITestOutputParser? parser, JobRunnerSettings settings)
    {
        this._masker = masker;
        this._parser = parser;
        this._settings = settings;
        this._tail = new(settings.TailLines);
        this._writer = new(stream: OpenLog(logPath), encoding: new UTF8Encoding(false));
    }

    public async ValueTask DisposeAsync()
    {
        await this._fileLock.WaitAsync(CancellationToken.None);

        try
        {
            await this._writer.DisposeAsync();
        }
        finally
        {
            this._fileLock.Release();
        }

        this._fileLock.Dispose();
    }

    public ValueTask WriteHeaderAsync(string text)
    {
        return this.WriteAsync(raw: text, observe: false);
    }

    public ValueTask WriteLineAsync(string line)
    {
        return this.WriteAsync(raw: line, observe: true);
    }

    public IReadOnlyList<string> Tail()
    {
        lock (this._tailLock)
        {
            return [.. this._tail];
        }
    }

    private async ValueTask WriteAsync(string raw, bool observe)
    {
        string line = this.Clean(raw);

        lock (this._tailLock)
        {
            if (observe)
            {
                this._parser?.Observe(line);
            }

            this.AddToTail(line);
        }

        // Log writes are never cancelled: the log has to record how a cancelled or timed-out job ended
        await this._fileLock.WaitAsync(CancellationToken.None);

        try
        {
            await this.AppendToLogAsync(line);
        }
        finally
        {
            this._fileLock.Release();
        }
    }

    private string Clean(string raw)
    {
        string line = this._masker.Mask(AnsiEscape().Replace(input: raw, replacement: string.Empty));

        return line.Length > this._settings.MaxLineLength
            ? string.Concat(str0: line.AsSpan(start: 0, length: this._settings.MaxLineLength), str1: TRUNCATED_LINE)
            : line;
    }

    private void AddToTail(string line)
    {
        if (this._tail.Count == this._settings.TailLines)
        {
            _ = this._tail.Dequeue();
        }

        this._tail.Enqueue(line);
    }

    private async ValueTask AppendToLogAsync(string line)
    {
        if (this._truncated)
        {
            return;
        }

        string text = line;

        if (this._written + line.Length + 1 > this._settings.MaxLogCharacters)
        {
            this._truncated = true;
            text = TRUNCATED_LOG;
        }

        this._written += text.Length + 1;
        await this._writer.WriteLineAsync(buffer: text.AsMemory(), cancellationToken: CancellationToken.None);
        await this._writer.FlushAsync(CancellationToken.None);
    }

    private static FileStream OpenLog(string logPath)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new(path: logPath, options: options);
    }

    [GeneratedRegex(pattern: @"\x1B\[[0-9;?]*[ -/]*[@-~]", options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AnsiEscape();
}
