using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Configuration;
using Credfeto.Build.Core.Configuration.Exceptions;
using Credfeto.Build.Core.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Configuration;

public sealed class BuildServerEnvironmentTests : TestBase
{
    private const string PRIVATE_FOLDER = "/tmp/credfeto-build-mcp-private";
    private const string MISSING_DEFAULT_ROOT = "/nonexistent/credfeto-build-mcp-tests/default-root";
    private const string GIT_ROOT = "/work/checkout";

    private const string NOT_IN_A_CHECKOUT_MESSAGE =
        "BUILD_MCP_REPO_ROOT is not set, "
        + MISSING_DEFAULT_ROOT
        + " does not exist and the current directory is not in a git checkout: set BUILD_MCP_REPO_ROOT to the repository's absolute path, or start the server inside the repository";

    private const string CREATE_FAILED_MESSAGE =
        "There is no home directory for the job logs, and a private folder could not be created in the temporary directory: set BUILD_MCP_LOG_DIR to an absolute path";

    [Fact]
    public async Task TheDefaultRootIsUsedWhenItExistsAndTheVariableIsUnsetAsync()
    {
        using TemporaryDirectory folder = new();

        BuildServerEnvironment environment = await BuildServerEnvironment.ResolveAsync(
            getVariable: static _ => null,
            createPrivateFolder: static () => PRIVATE_FOLDER,
            defaultRepositoryRoot: folder.Path,
            findGitRoot: NeverSearchedAsync,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: folder.Path, actual: environment.RepositoryRoot);
        Assert.Equal(expected: RepositoryRootSource.Default, actual: environment.RepositoryRootSource);
        Assert.Equal(expected: PRIVATE_FOLDER, actual: environment.LogDirectory);
    }

    [Fact]
    public async Task ASetRootIsUsedEvenWhenTheDefaultExistsAsync()
    {
        using TemporaryDirectory folder = new();

        BuildServerEnvironment environment = await BuildServerEnvironment.ResolveAsync(
            getVariable: static name =>
                StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE)
                    ? "/work/repo"
                    : null,
            createPrivateFolder: static () => PRIVATE_FOLDER,
            defaultRepositoryRoot: folder.Path,
            findGitRoot: NeverSearchedAsync,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "/work/repo", actual: environment.RepositoryRoot);
        Assert.Equal(expected: RepositoryRootSource.Variable, actual: environment.RepositoryRootSource);
    }

    [Fact]
    public async Task AnUnsetRootWithoutTheDefaultUsesTheGitRootAsync()
    {
        BuildServerEnvironment environment = await this.ResolveAsync();

        Assert.Equal(expected: GIT_ROOT, actual: environment.RepositoryRoot);
        Assert.Equal(expected: RepositoryRootSource.GitRoot, actual: environment.RepositoryRootSource);
    }

    [Fact]
    public async Task TheGitRootIsNormalisedAsync()
    {
        BuildServerEnvironment environment = await this.ResolveWithGitRootAsync(GIT_ROOT + "/sub/../");

        Assert.Equal(expected: GIT_ROOT, actual: environment.RepositoryRoot);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/checkout")]
    public async Task AnUnsetRootWithoutTheDefaultOutsideAGitCheckoutIsAnErrorAsync(string? gitRoot)
    {
        InvalidBuildConfigurationException exception = await Assert.ThrowsAsync<InvalidBuildConfigurationException>(
            async () =>
                _ = await this.ResolveWithGitRootAsync(gitRoot)
        );

        Assert.Equal(expected: NOT_IN_A_CHECKOUT_MESSAGE, actual: exception.Message);
    }

    [Fact]
    public static void TheRepositoryRootDescriptionNamesItsSource()
    {
        Assert.Equal(
            expected: "repository root /work/repo (from BUILD_MCP_REPO_ROOT)",
            actual: Describe(RepositoryRootSource.Variable)
        );
        Assert.Equal(
            expected: "repository root /work/repo (the default; BUILD_MCP_REPO_ROOT is not set)",
            actual: Describe(RepositoryRootSource.Default)
        );
        Assert.Equal(
            expected: "repository root /work/repo (the git root of the current directory; BUILD_MCP_REPO_ROOT is not set and the default folder does not exist)",
            actual: Describe(RepositoryRootSource.GitRoot)
        );
    }

    [Fact]
    public async Task AbsoluteVariablesAreUsedAsync()
    {
        BuildServerEnvironment environment = await this.ResolveAsync(
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
    public async Task ARootThatIsSetButNotAbsoluteIsAnErrorAsync(string value)
    {
        InvalidBuildConfigurationException exception = await Assert.ThrowsAsync<InvalidBuildConfigurationException>(
            async () =>
                _ = await this.ResolveAsync((BuildServerEnvironment.REPOSITORY_ROOT_VARIABLE, value))
        );

        Assert.Equal(
            expected: "BUILD_MCP_REPO_ROOT is set to \""
                + value
                + "\", which is not an absolute path: set it to an absolute path, or unset it to use "
                + MISSING_DEFAULT_ROOT
                + " (or, when that does not exist, the git root of the current directory)",
            actual: exception.Message
        );
    }

    [Theory]
    [InlineData("relative/logs")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ALogDirectoryThatIsSetButNotAbsoluteIsAnErrorAsync(string value)
    {
        InvalidBuildConfigurationException exception = await Assert.ThrowsAsync<InvalidBuildConfigurationException>(
            async () =>
                _ = await this.ResolveAsync(
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
    public async Task AnUnsetRootFallsBackEvenWithAnAbsoluteLogDirectoryAsync()
    {
        BuildServerEnvironment environment = await this.ResolveAsync(
            (BuildServerEnvironment.LOG_DIRECTORY_VARIABLE, "/work/logs")
        );

        Assert.Equal(expected: GIT_ROOT, actual: environment.RepositoryRoot);
        Assert.Equal(expected: "/work/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public async Task StateHomeIsUsedWhenSetAsync()
    {
        BuildServerEnvironment environment = await this.ResolveAsync(
            (BuildServerEnvironment.STATE_HOME_VARIABLE, "/home/user/.state"),
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/home/user/.state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public async Task HomeIsUsedWhenStateHomeIsNotSetAsync()
    {
        BuildServerEnvironment environment = await this.ResolveAsync(
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/state")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RelativeOrBlankStateHomeIsIgnoredAsync(string value)
    {
        BuildServerEnvironment environment = await this.ResolveAsync(
            (BuildServerEnvironment.STATE_HOME_VARIABLE, value),
            (BuildServerEnvironment.HOME_VARIABLE, "/home/user")
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/home")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithoutAnAbsoluteHomeAPrivateFolderIsUsedAsync(string value)
    {
        BuildServerEnvironment environment = await this.ResolveAsync((BuildServerEnvironment.HOME_VARIABLE, value));

        Assert.Equal(expected: PRIVATE_FOLDER, actual: environment.LogDirectory);
    }

    [Theory]
    [InlineData("relative/state")]
    [InlineData("")]
    public async Task WithoutAnAbsoluteStateHomeOrHomeAPrivateFolderIsUsedAsync(string value)
    {
        BuildServerEnvironment environment = await this.ResolveAsync(
            (BuildServerEnvironment.STATE_HOME_VARIABLE, value),
            (BuildServerEnvironment.HOME_VARIABLE, "relative/home")
        );

        Assert.Equal(expected: PRIVATE_FOLDER, actual: environment.LogDirectory);
    }

    [Fact]
    public async Task DifferentHomesGetDifferentFoldersAsync()
    {
        BuildServerEnvironment first = await this.ResolveAsync((BuildServerEnvironment.HOME_VARIABLE, "/home/first"));
        BuildServerEnvironment second = await this.ResolveAsync((BuildServerEnvironment.HOME_VARIABLE, "/home/second"));

        Assert.NotEqual(expected: first.LogDirectory, actual: second.LogDirectory, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task TheProcessEnvironmentResolvesToAnAbsoluteLogDirectoryAsync()
    {
        BuildServerEnvironment environment = await BuildServerEnvironment.FromProcessAsync(this.CancellationToken());

        Assert.True(
            condition: Path.IsPathFullyQualified(environment.LogDirectory),
            userMessage: "The log directory should be absolute"
        );
    }

    [Fact]
    public async Task ThePrivateFolderIsNotCreatedWhenThereIsAHomeAsync()
    {
        BuildServerEnvironment environment = await this.ResolveWithAsync(
            getVariable: static name =>
                StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.HOME_VARIABLE) ? "/home/user" : null,
            createPrivateFolder: NeverCreated
        );

        Assert.Equal(expected: "/home/user/.local/state/credfeto-build-mcp/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public async Task ThePrivateFolderIsNotCreatedWhenTheLogDirectoryIsSetAsync()
    {
        BuildServerEnvironment environment = await this.ResolveWithAsync(
            getVariable: static name =>
                StringComparer.Ordinal.Equals(x: name, y: BuildServerEnvironment.LOG_DIRECTORY_VARIABLE)
                    ? "/work/logs"
                    : null,
            createPrivateFolder: NeverCreated
        );

        Assert.Equal(expected: "/work/logs", actual: environment.LogDirectory);
    }

    [Fact]
    public async Task ARelativeLogDirectoryWithoutAHomeNamesThePrivateFolderWithoutCreatingItAsync()
    {
        InvalidBuildConfigurationException exception = await Assert.ThrowsAsync<InvalidBuildConfigurationException>(
            async () =>
                _ = await this.ResolveWithAsync(
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
    public async Task APrivateFolderThatCannotBeCreatedIsAnErrorAsync()
    {
        IOException failure = new("No space left on device");

        InvalidBuildConfigurationException exception = await Assert.ThrowsAsync<InvalidBuildConfigurationException>(
            async () =>
                _ = await this.ResolveWithAsync(getVariable: static _ => null, createPrivateFolder: () => throw failure)
        );

        Assert.Equal(expected: CREATE_FAILED_MESSAGE, actual: exception.Message);
        Assert.Same(expected: failure, actual: exception.InnerException);
    }

    [Fact]
    public async Task APrivateFolderThatIsNotAllowedIsAnErrorAsync()
    {
        InvalidBuildConfigurationException exception = await Assert.ThrowsAsync<InvalidBuildConfigurationException>(
            async () =>
                _ = await this.ResolveWithAsync(
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

    private static string Describe(RepositoryRootSource source)
    {
        return new BuildServerEnvironment(
            RepositoryRoot: "/work/repo",
            RepositoryRootSource: source,
            LogDirectory: "/work/logs"
        ).DescribeRepositoryRoot();
    }

    private static ValueTask<string?> NeverSearchedAsync(CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("git should not be asked for the repository root");
    }

    private ValueTask<BuildServerEnvironment> ResolveAsync(params (string Name, string Value)[] values)
    {
        Dictionary<string, string> variables = [with(StringComparer.Ordinal)];

        foreach ((string name, string value) in values)
        {
            variables[name] = value;
        }

        return this.ResolveWithAsync(getVariable: variables.GetValueOrDefault, createPrivateFolder: static () =>
            PRIVATE_FOLDER
        );
    }

    private ValueTask<BuildServerEnvironment> ResolveWithGitRootAsync(string? gitRoot)
    {
        return BuildServerEnvironment.ResolveAsync(
            getVariable: static _ => null,
            createPrivateFolder: static () => PRIVATE_FOLDER,
            defaultRepositoryRoot: MISSING_DEFAULT_ROOT,
            findGitRoot: _ => ValueTask.FromResult(gitRoot),
            cancellationToken: this.CancellationToken()
        );
    }

    private ValueTask<BuildServerEnvironment> ResolveWithAsync(
        Func<string, string?> getVariable,
        Func<string> createPrivateFolder
    )
    {
        return BuildServerEnvironment.ResolveAsync(
            getVariable: getVariable,
            createPrivateFolder: createPrivateFolder,
            defaultRepositoryRoot: MISSING_DEFAULT_ROOT,
            findGitRoot: static _ => ValueTask.FromResult<string?>(GIT_ROOT),
            cancellationToken: this.CancellationToken()
        );
    }
}
