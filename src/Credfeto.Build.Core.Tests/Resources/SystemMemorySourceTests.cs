using System.IO;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Resources;

public sealed class SystemMemorySourceTests : TestBase
{
    private readonly SystemMemorySource _source = new();

    [Fact]
    public void AFileIsReadWhole()
    {
        using TemporaryDirectory directory = new();
        string path = Path.Combine(path1: directory.Path, path2: "memory.max");
        File.WriteAllText(path: path, contents: "4294967296\n");

        Assert.Equal(expected: "4294967296\n", actual: this._source.ReadFile(path));
    }

    [Fact]
    public void AMissingFileReadsAsNothing()
    {
        using TemporaryDirectory directory = new();

        Assert.Null(this._source.ReadFile(Path.Combine(path1: directory.Path, path2: "missing", path3: "memory.max")));
    }

    [Fact]
    public void ADirectoryReadsAsNothing()
    {
        using TemporaryDirectory directory = new();

        Assert.Null(this._source.ReadFile(directory.Path));
    }
}
