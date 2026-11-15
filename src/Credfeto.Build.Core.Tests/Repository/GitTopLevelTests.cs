using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Repository;

public sealed class GitTopLevelTests : TestBase
{
    [Fact]
    public async Task AFolderInsideACheckoutGivesTheCheckoutRootAsync()
    {
        using TemporaryDirectory repository = new();
        Assert.NotNull(
            await GitCommand.TryRunAsync(
                repositoryRoot: repository.Path,
                arguments: ["init", "--quiet"],
                cancellationToken: this.CancellationToken()
            )
        );
        string nested = Directory.CreateDirectory(repository.Combine("src/nested")).FullName;

        string? topLevel = await GitTopLevel.FindAsync(directory: nested, cancellationToken: this.CancellationToken());

        Assert.Equal(expected: repository.Path, actual: topLevel);
    }

    [Fact]
    public async Task AFolderOutsideACheckoutGivesNullAsync()
    {
        using TemporaryDirectory folder = new();

        string? topLevel = await GitTopLevel.FindAsync(
            directory: folder.Path,
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(topLevel);
    }

    [Fact]
    public async Task AMissingFolderGivesNullAsync()
    {
        using TemporaryDirectory folder = new();

        string? topLevel = await GitTopLevel.FindAsync(
            directory: folder.Combine("missing"),
            cancellationToken: this.CancellationToken()
        );

        Assert.Null(topLevel);
    }
}
