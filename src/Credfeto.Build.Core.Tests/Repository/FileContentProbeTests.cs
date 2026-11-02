using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class FileContentProbeTests : TestBase
{
    [Fact]
    public async Task ReportsOnlyTheFilesWhoseContentChangedAsync()
    {
        using TemporaryDirectory repository = new();
        string same = await repository.WriteAsync(relative: "src/Same.cs", content: "class Same {}", cancellationToken: this.CancellationToken());
        string changed = await repository.WriteAsync(relative: "src/Changed.cs", content: "class Changed{}", cancellationToken: this.CancellationToken());
        string deleted = await repository.WriteAsync(relative: "src/Deleted.cs", content: "class Deleted {}", cancellationToken: this.CancellationToken());

        FileContentProbe probe = await FileContentProbe.SnapshotAsync(repositoryRoot: repository.Path, files: [same, changed, deleted], cancellationToken: this.CancellationToken());

        await File.WriteAllTextAsync(path: changed, contents: "class Changed { }", cancellationToken: this.CancellationToken());
        await File.WriteAllTextAsync(path: same, contents: "class Same {}", cancellationToken: this.CancellationToken());
        File.Delete(deleted);

        IReadOnlyList<string> result = await probe.CollectAsync(this.CancellationToken());

        Assert.Equal(expected: ["src/Changed.cs", "src/Deleted.cs"], actual: result, comparer: StringComparer.Ordinal);
    }
}
