using System;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Commands;

// Finds python3 and the other tools on PATH as absolute paths, so a job never runs whatever a relative PATH entry or
// the working directory happens to hold
public sealed class PythonLocator
{
    public const string PYTHON = "python3";
    public const string UV = "uv";
    public const string FLAKE8 = "flake8";
    public const string PYLINT = "pylint";

    private readonly Func<string, string?> _find;

    public PythonLocator(Func<string, string?> find)
    {
        this._find = find;
    }

    public static PythonLocator FromProcess()
    {
        return new(static name =>
            ExecutableSearch.Find(name: name, searchPath: Environment.GetEnvironmentVariable("PATH"))
        );
    }

    public string? Find()
    {
        return this._find(PYTHON);
    }

    // The tool's absolute path; the name is one of the constants above, never caller input
    public string Require(string name)
    {
        return this._find(name) ?? throw new ToolUnavailableException(name + " was not found on PATH");
    }
}
