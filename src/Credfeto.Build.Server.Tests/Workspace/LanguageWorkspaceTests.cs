using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tests.Helpers;
using Credfeto.Build.Server.Workspace;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Server.Tests.Workspace;

public sealed class LanguageWorkspaceTests : TestBase
{
    private static readonly FakeDetection Initial = new(Signature: "one", ToolNames: ["detect", "build"]);

    [Fact]
    public static void TheInitialDetectionsToolsAreOffered()
    {
        using LanguageWorkspace<FakeDetection> workspace = new(initial: Initial, language: new FakeLanguage(Initial));

        Assert.Equal(expected: ["build", "detect"], actual: Names(workspace), comparer: StringComparer.Ordinal);
        Assert.Same(expected: Initial, actual: workspace.Current);
    }

    [Fact]
    public async Task AnUnchangedSignatureChangesNothingAsync()
    {
        FakeLanguage language = new(Initial with { ToolNames = ["other"] });
        using LanguageWorkspace<FakeDetection> workspace = new(initial: Initial, language: language);
        ChangeCounter changes = ChangeCounter.Watch(workspace.Tools);

        FakeDetection fresh = await workspace.RefreshAsync(this.CancellationToken());

        Assert.Same(expected: language.Next, actual: fresh);
        Assert.Same(expected: Initial, actual: workspace.Current);
        Assert.Equal(expected: 0, actual: changes.Count);
        Assert.Equal(expected: ["build", "detect"], actual: Names(workspace), comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task AChangedSignatureSwapsEveryToolInOneChangeAsync()
    {
        FakeLanguage language = new(new(Signature: "two", ToolNames: ["detect", "test", "coverage"]));
        using LanguageWorkspace<FakeDetection> workspace = new(initial: Initial, language: language);
        ChangeCounter changes = ChangeCounter.Watch(workspace.Tools);

        FakeDetection fresh = await workspace.RefreshAsync(this.CancellationToken());

        Assert.Same(expected: language.Next, actual: workspace.Current);
        Assert.Same(expected: fresh, actual: workspace.Current);
        Assert.Equal(expected: 1, actual: changes.Count);
        Assert.Equal(
            expected: ["coverage", "detect", "test"],
            actual: Names(workspace),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task ARepositoryChangeDetectsAgainAsync()
    {
        FakeLanguage language = new(new(Signature: "none", ToolNames: []));
        using LanguageWorkspace<FakeDetection> workspace = new(initial: Initial, language: language);

        await workspace.RepositoryChangedAsync(this.CancellationToken());

        Assert.Equal(expected: 1, actual: language.Detections);
        Assert.True(condition: workspace.Tools.IsEmpty, userMessage: "the tools went with the language");
    }

    [Fact]
    public static void TheInitialToolsAreInNameOrder()
    {
        FakeDetection outOfOrder = new(Signature: "one", ToolNames: ["zeta", "alpha", "Mike", "Bravo", "job_status"]);
        using LanguageWorkspace<FakeDetection> workspace = new(
            initial: outOfOrder,
            language: new FakeLanguage(outOfOrder)
        );

        Assert.Equal(
            expected: ["alpha", "Bravo", "job_status", "Mike", "zeta"],
            actual: OfferedNames(workspace),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task ARefreshedToolListIsInNameOrderAsync()
    {
        FakeLanguage language = new(
            new(Signature: "two", ToolNames: ["yankee", "charlie", "Xray", "Delta", "job_result"])
        );
        using LanguageWorkspace<FakeDetection> workspace = new(initial: Initial, language: language);

        _ = await workspace.RefreshAsync(this.CancellationToken());

        Assert.Equal(
            expected: ["charlie", "Delta", "job_result", "Xray", "yankee"],
            actual: OfferedNames(workspace),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void TheLanguageDecidesWhichFailuresAreTransient()
    {
        using LanguageWorkspace<FakeDetection> workspace = new(initial: Initial, language: new FakeLanguage(Initial));

        Assert.True(
            condition: workspace.IsTransientFailure(new InvalidDataException("mid-change")),
            userMessage: "the fake language's transient failure"
        );
        Assert.False(
            condition: workspace.IsTransientFailure(new InvalidOperationException("broken")),
            userMessage: "anything else is not transient"
        );
    }

    private static string[] Names(LanguageWorkspace<FakeDetection> workspace)
    {
        return [.. workspace.Tools.Select(static tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal)];
    }

    private static string[] OfferedNames(LanguageWorkspace<FakeDetection> workspace)
    {
        return [.. workspace.Tools.Select(static tool => tool.ProtocolTool.Name)];
    }
}
