using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Credfeto.Build.Core.Coverage;
using Credfeto.Build.Core.Coverage.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Coverage;

public sealed class CoberturaParserTests : TestBase
{
    [Fact]
    public async Task CountsEachLineOnceAndCoveredWhenAnyEntryWasHitAsync()
    {
        CoberturaReport report = await this.ParseAsync(CoberturaSamples.ONE_PACKAGE);

        CoberturaPackage package = Assert.Single(report.Packages);
        Assert.Equal(expected: "Example.Core", actual: package.Name);
        Assert.Equal(expected: 5, actual: package.Lines.Count);
        Assert.Equal(expected: 3, actual: package.Lines.Count(static pair => pair.Value));
        Assert.True(condition: package.Lines[new(File: "/src/Example.Core/Alpha.cs", Number: 12)], userMessage: "Alpha.cs:12 was hit");
        Assert.False(condition: package.Lines[new(File: "/src/Example.Core/Beta.cs", Number: 6)], userMessage: "Beta.cs:6 was not hit");
    }

    [Fact]
    public async Task ReadsEveryPackageAsync()
    {
        CoberturaReport report = await this.ParseAsync(CoberturaSamples.OTHER_PACKAGE);

        Assert.Equal(expected: ["Example.Core", "Example.Other"], actual: report.Packages.Select(static package => package.Name).Order(StringComparer.Ordinal), comparer: StringComparer.Ordinal);
    }

    [Fact]
    public Task MalformedXmlIsRejectedAsync()
    {
        return Assert.ThrowsAsync<InvalidCoberturaException>(() => this.ParseAsync("<coverage><packages>").AsTask());
    }

    [Fact]
    public Task DocumentWithoutCoverageRootIsRejectedAsync()
    {
        return Assert.ThrowsAsync<InvalidCoberturaException>(() => this.ParseAsync("<report />").AsTask());
    }

    [Fact]
    public Task DtdIsRefusedAsync()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE coverage [<!ENTITY x SYSTEM "file:///etc/passwd">]>
            <coverage><packages /></coverage>
            """;

        return Assert.ThrowsAsync<InvalidCoberturaException>(() => this.ParseAsync(xml).AsTask());
    }

    [Fact]
    public Task NonNumericLineIsRejectedAsync()
    {
        const string xml = """
            <coverage><packages><package name="P"><classes><class filename="a.cs"><lines>
            <line number="x" hits="1" />
            </lines></class></classes></package></packages></coverage>
            """;

        return Assert.ThrowsAsync<InvalidCoberturaException>(() => this.ParseAsync(xml).AsTask());
    }

    private async ValueTask<CoberturaReport> ParseAsync(string xml)
    {
        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));

        return await CoberturaParser.ParseAsync(stream: stream, cancellationToken: this.CancellationToken());
    }
}
