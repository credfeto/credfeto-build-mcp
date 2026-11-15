using System;
using System.Collections.Generic;
using System.IO;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Configuration.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Configuration;

public sealed class BuildServerEnvironmentTests : TestBase
{
    private const string PRIVATE_FOLDER = "/tmp/credfeto-build-mcp-private";

    private const string CREATE_FAILED_MESSAGE =
        "There is no home directory for the job logs, and a private folder could not be created in the temporary directory: set BUILD_MCP_LOG_DIR to an absolute path";

    [Fact]
    public static void DefaultsApplyWhenNothingIsSet()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(
            getVariable: static _ => null,
            createPrivateFolder: static () => PRIVATE_FOLDER
        );

        Assert.Equal(expected: BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT, actual: environment.RepositoryRoot);
        Assert.Equal(expected: PRIVATE_FOLDER, actual: environment.LogDirectory);
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
    public static void ARootThatIsSetButNotAbsoluteIsAnError(string value)
    {
        InvalidBuildConfigurationException exception = Assert.Throws<InvalidBuildConfigurationException>(() =>
            Resolve((BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE, value))
        );

        Assert.Equal(
            expected: "BUILD_MCP_REPO_ROOT is set to \""
                + value
                + "\", which is not an absolute path: set it to an absolute path, or unset it to use /workspace/repo",
            actual: exception.Message
        );
    }

    [Theory]
    [InlineData("relative/logs")]
    [InlineData("")]
    [InlineData("   ")]
    public static void ALogDirectoryThatIsSetButNotAbsoluteIsAnError(string value)
    {
        InvalidBuildConfigurationException exception = Assert.Throws<InvalidBuildConfigurationException>(() =>
            Resolve(
                (BuildServerEnvironment.LOG_DIRECTORY_VARIABLE, value),
                (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
            )
        );

        Assert.Equal(
            expected: "BUILD_MCP_LOG_DIR is set to \""
                + value
                + "\", which is not an absolute path: set it to an absolute path, or unset it to use /home/user/.local/state/credfeto-build-mcp/logs",
            actual: exception.Message
        );
    }

    [Fact]
    public static void AnUnsetRootUsesTheDefaultEvenWithAnAbsoluteLogDirectory()
    {
        BuildServerEnvironment environment = Resolve((BuildServerEnvironment.LOG_DIRECTORY_VARIABLE, "/work/logs"));

        Assert.Equal(expected: BuildServerEnvironment.DEFAULT_REPOSITORY_ROOT, actual: environment.RepositoryRoot);
        Assert.Equal(expected: "/work/logs", actual: environment.LogDirectory);
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
    public static void WithoutAnAbsoluteHomeAPrivateFolderIsUsed(string value)
    {
        BuildServerEnvironment environment = Resolve((BuildServerEnvironment.HOME_VARIABLE, value));

        Assert.Equal(expected: PRIVATE_FOLDER, actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/state")]
    [InlineData("")]
    public static void WithoutAnAbsoluteStateHomeOrHomeAPrivateFolderIsUsed(string value)
    {
        BuildServerEnvironment environment = Resolve(
            (BuildServerEnvironment.STATE_HOME_VARIABLE, value),
            (BuildServerEnvironment.HOME_VARIABLE, "relative/home")
        );

        Assert.Equal(expected: PRIVATE_FOLDER, actual: environment.LogDirectory);
    }

    [Fact]
    public static void DifferentHomesGetDifferentFolders()
    {
        BuildServerEnvironment first = Resolve((BuildServerEnvironment.HOME_VARIABLE, "/home/first"));
        BuildServerEnvironment second = Resolve((BuildServerEnvironment.HOME_VARIABLE, "/home/second"));

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
    public static void ThePrivateFolderIsNotCreatedWhenThereIsAHome()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(
            getVariable: static name =>
                StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.HOME_VARIABLE) ? "/home/user" : null,
            createPrivateFolder: NeverCreated
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public static void ThePrivateFolderIsNotCreatedWhenTheLogDirectoryIsSet()
    {
        BuildServerEnvironment environment = BuildServerEnvironment.Resolve(
            getVariable: static name =>
                StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.LOG_DIRECTORY_VARIABLE)
                    ? "/work/logs"
                    : null,
            createPrivateFolder: NeverCreated
        );

        Assert.Equal(expected: "/work/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public static void ARelativeLogDirectoryWithoutAHomeNamesThePrivateFolderWithoutCreatingIt()
    {
        InvalidBuildConfigurationException exception = Assert.Throws<InvalidBuildConfigurationException>(() =>
            BuildServerEnvironment.Resolve(
                getVariable: static name =>
                    StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.LOG_DIRECTORY_VARIABLE)
                        ? "relative/logs"
                        : null,
                createPrivateFolder: NeverCreated
            )
        );

        Assert.Equal(
            expected: "BUILD_MCP_LOG_DIR is set to \"relative/logs\", which is not an absolute path: set it to an absolute path, or unset it to use a new private folder in the temporary directory",
            actual: exception.Message
        );
    }

    [Fact]
    public static void APrivateFolderThatCannotBeCreatedIsAnError()
    {
        IOException failure = new("No space left on device");

        InvalidBuildConfigurationException exception = Assert.Throws<InvalidBuildConfigurationException>(() =>
            BuildServerEnvironment.Resolve(getVariable: static _ => null, createPrivateFolder: () => throw failure)
        );

        Assert.Equal(expected: CREATE_FAILED_MESSAGE, actual: exception.Message);
        Assert.Same(expected: failure, actual: exception.InnerException);
    }

    [Fact]
    public static void APrivateFolderThatIsNotAllowedIsAnError()
    {
        InvalidBuildConfigurationException exception = Assert.Throws<InvalidBuildConfigurationException>(() =>
            BuildServerEnvironment.Resolve(
                getVariable: static _ => null,
                createPrivateFolder: static () => throw new UnauthorizedAccessException("Permission denied")
            )
        );

        Assert.Equal(expected: CREATE_FAILED_MESSAGE, actual: exception.Message);
    }

    [Fact]
    public static void EachPrivateFolderIsNewAndOwnerOnly()
    {
        string first = BuildServerEnvironment.CreatePrivateTemporaryFolder();

        try
        {
            string second = BuildServerEnvironment.CreatePrivateTemporaryFolder();

            try
            {
                Assert.NotEqual(expected: first, actual: second, comparer: StringComparer.Ordinal);
                AssertPrivateFolder(first);
                AssertPrivateFolder(second);
            }
            finally
            {
                Directory.Delete(second);
            }
        }
        finally
        {
            Directory.Delete(first);
        }
    }

    private static void AssertPrivateFolder(string folder)
    {
        DirectoryInfo info = new(folder);

        Assert.True(condition: info.Exists, userMessage: "The private folder should exist");
        Assert.Null(info.LinkTarget);
        Assert.Equal(
            expected: Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
            actual: info.Parent?.FullName,
            comparer: StringComparer.Ordinal
        );
        Assert.StartsWith(
            expectedStartString: "credfeto-build-mcp-",
            actualString: info.Name,
            comparisonType: StringComparison.Ordinal
        );

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                expected: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                actual: info.UnixFileMode
            );
        }
    }

    private static string NeverCreated()
    {
        throw new InvalidOperationException("The private folder should not be created");
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
            createPrivateFolder: static () => PRIVATE_FOLDER
        );
    }
}
