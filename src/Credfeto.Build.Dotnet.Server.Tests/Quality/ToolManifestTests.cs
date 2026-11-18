using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Dotnet.Server.Tests.Quality;

public sealed class ToolManifestTests : TestBase, IDisposable
{
    private const string MANIFEST = """{ "version": 1, "isRoot": true, "tools": {} }""";
    private const string REPOSITORY = "checkouts/repo";

    private readonly TemporaryDirectory _tree = new();

    public void Dispose()
    {
        this._tree.Dispose();
    }

    [Theory]
    [InlineData(".config/dotnet-tools.json")]
    [InlineData("dotnet-tools.json")]
    public async Task AManifestInTheRepositoryRootIsFoundAsync(string location)
    {
        string manifest = await this.WriteManifestAsync(REPOSITORY + "/" + location);

        Assert.Equal(expected: manifest, actual: this.Find(REPOSITORY));
    }

    [Theory]
    [InlineData(".config/dotnet-tools.json")]
    [InlineData("dotnet-tools.json")]
    public async Task AManifestOnlyInAParentDirectoryIsFoundAsync(string location)
    {
        string manifest = await this.WriteManifestAsync(location);

        Assert.Equal(expected: manifest, actual: this.Find(REPOSITORY));
    }

    [Fact]
    public async Task TheConfigManifestComesFirstInADirectoryAsync()
    {
        string inConfig = await this.WriteManifestAsync(REPOSITORY + "/.config/dotnet-tools.json");
        _ = await this.WriteManifestAsync(REPOSITORY + "/dotnet-tools.json");

        Assert.Equal(expected: inConfig, actual: this.Find(REPOSITORY));
    }

    [Fact]
    public async Task TheNearestManifestWinsAsync()
    {
        _ = await this.WriteManifestAsync(".config/dotnet-tools.json");
        string nearest = await this.WriteManifestAsync(REPOSITORY + "/dotnet-tools.json");

        Assert.Equal(expected: nearest, actual: this.Find(REPOSITORY));
    }

    [Fact]
    public async Task ARepositoryRootWithATrailingSeparatorIsSearchedAsync()
    {
        string manifest = await this.WriteManifestAsync("dotnet-tools.json");

        Assert.Equal(expected: manifest, actual: this.Find(REPOSITORY + "/"));
    }

    [Fact]
    public void NoManifestAnywhereUpTheTreeFindsNothing()
    {
        _ = Directory.CreateDirectory(this._tree.Combine(REPOSITORY));

        Assert.Null(this.Find(REPOSITORY));
    }

    [Fact]
    public async Task AManifestBelowTheRepositoryRootIsNotFoundAsync()
    {
        _ = await this.WriteManifestAsync(REPOSITORY + "/src/dotnet-tools.json");

        Assert.Null(this.Find(REPOSITORY));
    }

    private string? Find(string relative)
    {
        // Only files inside the temporary tree count, so a manifest above the temp directory can't leak in
        return ToolManifest.Find(repositoryRoot: this._tree.Combine(relative), fileExists: this.ExistsInTree);
    }

    private bool ExistsInTree(string path)
    {
        return path.StartsWith(this._tree.Path + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && File.Exists(path);
    }

    private ValueTask<string> WriteManifestAsync(string relative)
    {
        return this._tree.WriteAsync(
            relative: relative,
            content: MANIFEST,
            cancellationToken: this.CancellationToken()
        );
    }
}
