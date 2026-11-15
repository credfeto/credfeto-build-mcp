using System;
using System.IO;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Quality;

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

    public string Require()
    {
        string? configured = this._getVariable(VARIABLE);

        // Set but blank is a misconfiguration like any other value, not a request for the PATH lookup
        if (configured is not null)
        {
            return Path.IsPathFullyQualified(configured) && ExecutableSearch.IsExecutable(configured)
                ? configured
                : throw new ToolUnavailableException(
                    VARIABLE + " is set but is not the absolute path of an executable file"
                );
        }

        return ExecutableSearch.Find(name: SCRIPT_NAME, searchPath: this._getVariable("PATH"))
            ?? throw new ToolUnavailableException(
                "The buildtest script was not found: set "
                    + VARIABLE
                    + " to its absolute path or put "
                    + SCRIPT_NAME
                    + " on PATH"
            );
    }
}
