using System;
using System.Collections.Generic;
using System.IO;
using Credfeto.Build.Core.Configuration;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Configuration;

public sealed class BuildServerEnvironmentTests : TestBase
{
    [Fact]
    public static void DefaultsApplyWhenNothingIsSet()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(static _ => null);

        Assert.Equal(expected: BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT, actual: environment.RepositoryRoot);
        Assert.Equal(
            expected: Path.TrimEndingDirectorySeparator(Path.Combine(path1: Path.GetTempPath(), path2: "credfeto-build-mcp")),
            actual: environment.LogDirectory
        );
    }

    [Fact]
    public static void AbsoluteVariablesAreUsed()
    {
        Dictionary<string, string> variables = new(StringComparer.Ordinal)
        {
            [BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE] = "/work/repo/",
            [BuildServerEnvironment.LOG_DIRECTORY_VARIABLE] = "/work/logs",
        };

        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(name => variables.GetValueOrDefault(name));

        Assert.Equal(expected: "/work/repo", actual: environment.RepositoryRoot);
        Assert.Equal(expected: "/work/logs", actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/repo")]
    [InlineData("")]
    [InlineData("   ")]
    public static void RelativeOrBlankRootFallsBackToTheDefault(string value)
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(name =>
            StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE) ? value : null
        );

        Assert.Equal(expected: BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT, actual: environment.RepositoryRoot);
    }
}
