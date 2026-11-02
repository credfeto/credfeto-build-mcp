using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Tests.Helpers;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        this.Path = System.IO.Path.Combine(
            path1: System.IO.Path.GetTempPath(),
            path2: "credfeto-build-mcp-tests",
            path3: Guid.NewGuid().ToString(format: "N", provider: CultureInfo.InvariantCulture)
        );
        _ = Directory.CreateDirectory(this.Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(this.Path))
        {
            Directory.Delete(path: this.Path, recursive: true);
        }
    }

    public string Combine(string relative)
    {
        return System.IO.Path.Combine(path1: this.Path, path2: relative);
    }

    public async ValueTask<string> WriteAsync(string relative, string content, CancellationToken cancellationToken)
    {
        string path = this.Combine(relative);
        string? directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path: path, contents: content, cancellationToken: cancellationToken);

        return path;
    }
}
