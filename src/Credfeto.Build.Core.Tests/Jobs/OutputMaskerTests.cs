using System.Collections.Generic;
using Credfeto.Build.Core.Jobs.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Jobs;

public sealed class OutputMaskerTests : TestBase
{
    private static readonly OutputMasker Masker = new(
        new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["NUGET_API_KEY"] = "s3cr3t-value-123",
            ["MY_TOKEN"] = "short",
            ["PATH"] = "/usr/bin:/bin:/usr/local/bin",
        }
    );

    [Theory]
    [InlineData("pushing with s3cr3t-value-123 now", "pushing with *** now")]
    [InlineData("token ghp_abcdefghijklmnopqrstuvwxyz0123", "token ***")]
    [InlineData("github_pat_11ABCDEFG0123456789_abcdefghijkl", "***")]
    [InlineData("Authorization: Bearer abc.def.ghi-jkl", "Authorization: Bearer ***")]
    [InlineData("password=hunter2hunter2; next", "password=***; next")]
    [InlineData("ApiKey: 12345678", "ApiKey: ***")]
    [InlineData("https://user:pa55w0rd@example.com/feed", "https://***@example.com/feed")]
    public static void SecretsAreMasked(string line, string expected)
    {
        Assert.Equal(expected: expected, actual: Masker.Mask(line));
    }

    [Theory]
    [InlineData("Build succeeded.")]
    [InlineData("short values like short are not masked")]
    [InlineData("/usr/bin:/bin:/usr/local/bin")]
    [InlineData("https://example.com/feed")]
    public static void OrdinaryOutputIsUnchanged(string line)
    {
        Assert.Equal(expected: line, actual: Masker.Mask(line));
    }
}
