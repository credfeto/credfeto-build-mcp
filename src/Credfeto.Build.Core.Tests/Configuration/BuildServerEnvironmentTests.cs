using System;
using System.Collections.Generic;
using System.IO;
using Credfeto.Build.Core.Configuration;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Configuration;

public sealed class BuildServerEnvironmentTests : TestBase
{
    private const string CURRENT_USER = "1234";

    [Fact]
    public static void DefaultsApplyWhenNothingIsSet()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(
            getVariable: static _ => null,
            currentUser: static () => CURRENT_USER
        );

        Assert.Equal(expected: BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT, actual: environment.RepositoryRoot);
        Assert.Equal(expected: TemporaryFolderFor(CURRENT_USER), actual: environment.LogDirectory);
    }

    [Fact]
    public static void AbsoluteVariablesAreUsed()
    {
        BuildServerEnvironment environment = Resolve(
            (BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE, "/work/repo/"),
            (BuildServerEnvironment.LOG_DIRECTORY_VARIABLE, "/work/logs"),
            (BuildServerEnvironment.STATE_HOME_VARIABLE, "/home/user/.state"),
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/work/repo", actual: environment.RepositoryRoot);
        Assert.Equal(expected: "/work/logs", actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/repo")]
    [InlineData("")]
    [InlineData("   ")]
    public static void RelativeOrBlankRootFallsBackToTheDefault(string value)
    {
        BuildServerEnvironment environment = Resolve((BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE, value));

        Assert.Equal(expected: BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT, actual: environment.RepositoryRoot);
    }

    [Fact]
    public static void StateHomeIsUsedWhenSet()
    {
        BuildServerEnvironment environment = Resolve(
            (BuildServerEnvironment.STATE_HOME_VARIABLE, "/home/user/.state"),
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/home/user/.state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public static void HomeIsUsedWhenStateHomeIsNotSet()
    {
        BuildServerEnvironment environment = Resolve((BuildServerEnvironment.HOME_VARIABLE, "/home/user"));

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/state")]
    [InlineData("")]
    [InlineData("   ")]
    public static void RelativeOrBlankStateHomeIsIgnored(string value)
    {
        BuildServerEnvironment environment = Resolve(
            (BuildServerEnvironment.STATE_HOME_VARIABLE, value),
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/home")]
    [InlineData("")]
    [InlineData("   ")]
    public static void WithoutAnAbsoluteHomeTheTemporaryFolderIsPerUser(string value)
    {
        BuildServerEnvironment environment = Resolve((BuildServerEnvironment.HOME_VARIABLE, value));

        Assert.Equal(expected: TemporaryFolderFor(CURRENT_USER), actual: environment.LogDirectory);
    }

    [Fact]
    public static void RelativeLogDirectoryFallsBackToThePerUserDefault()
    {
        BuildServerEnvironment environment = Resolve(
            (BuildServerEnvironment.LOG_DIRECTORY_VARIABLE, "relative/logs"),
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public static void DifferentHomesGetDifferentFolders()
    {
        BuildServerEnvironment first = Resolve((BuildServerEnvironment.HOME_VARIABLE, "/home/first"));
        BuildServerEnvironment second = Resolve((BuildServerEnvironment.HOME_VARIABLE, "/home/second"));

        Assert.NotEqual(expected: first.LogDirectory, actual: second.LogDirectory, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public static void DifferentUsersWithoutAHomeGetDifferentFolders()
    {
        BuildServerEnvironment first = BuildServerEnvironment.Resolve(
            getVariable: static _ => null,
            currentUser: static () => "1000"
        );
        BuildServerEnvironment second = BuildServerEnvironment.Resolve(
            getVariable: static _ => null,
            currentUser: static () => "1001"
        );

        Assert.NotEqual(expected: first.LogDirectory, actual: second.LogDirectory, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public static void TheProcessEnvironmentResolvesToAnAbsoluteLogDirectory()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.FromProcess();

        Assert.True(
            condition: Path.IsPathFullyQualified(environment.LogDirectory),
            userMessage: "The log directory should be absolute"
        );
    }

    [Fact]
    public static void TheUserIsOnlyLookedUpWhenThereIsNoHome()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(
            getVariable: static name =>
                StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.HOME_VARIABLE) ? "/home/user" : null,
            currentUser: static () => throw new InvalidOperationException("The user should not be looked up")
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public static void TheProcessUserIsKnown()
    {
        string user = BuildServerEnvironment.CurrentUser();

        Assert.False(condition: string.IsNullOrWhiteSpace(user), userMessage: "The current user should be known");
    }

    private static BuildServerEnvironment Resolve(params (string Name, string Value)[] values)
    {
        Dictionary<string, string> variables = [with(StringComparer.Ordinal)];

        foreach ((string name, string value) in values)
        {
            variables[name] = value;
        }

        return BuildServerEnvironment.Resolve(
            getVariable: variables.GetValueOrDefault,
            currentUser: static () => CURRENT_USER
        );
    }

    private static string TemporaryFolderFor(string user)
    {
        return Path.TrimEndingDirectorySeparator(
            Path.Combine(path1: Path.GetTempPath(), path2: "credfeto-build-mcp-" + user)
        );
    }
}
