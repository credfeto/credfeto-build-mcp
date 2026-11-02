using System;
using System.IO;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Quality;

/// <summary>
///     Finds the installed pre-commit buildtest script: <c>BUILD_MCP_BUILDTEST</c> when set (an absolute path),
///     otherwise <c>buildtest</c> on <c>PATH</c>. It is looked up on every call, so installing it needs no restart.
/// </summary>
public sealed class BuildtestLocator
{
    public const string VARIABLE = "BUILD_MCP_BUILDTEST";
    public const string SCRIPT_NAME = "buildtest";

    private readonly Func<string, string?> _getVariable;

    public BuildtestLocator(Func<string, string?> getVariable)
    {
        this._getVariable = getVariable;
    }

    public static BuildtestLocator FromProcess()
    {
        return new(Environment.GetEnvironmentVariable);
    }

    /// <summary>The script's absolute path; a clear failure when it is not installed.</summary>
    public string Require()
    {
        string? configured = this._getVariable(VARIABLE);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathFullyQualified(configured) && ExecutableSearch.IsExecutable(configured)
                ? configured
                : throw new ToolUnavailableException(VARIABLE + " is set but is not the absolute path of an executable file");
        }

        return ExecutableSearch.Find(name: SCRIPT_NAME, searchPath: this._getVariable("PATH"))
            ?? throw new ToolUnavailableException(
                "The buildtest script was not found: set " + VARIABLE + " to its absolute path or put " + SCRIPT_NAME + " on PATH"
            );
    }
}
