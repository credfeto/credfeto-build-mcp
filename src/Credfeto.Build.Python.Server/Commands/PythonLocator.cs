using System;
using Credfeto.Build.Core.Repository;

namespace Credfeto.Build.Python.Server.Commands;

// Finds python3 on PATH as an absolute path, so a job never runs whatever a relative PATH entry or the working
// directory happens to hold
public sealed class PythonLocator
{
    public const string PYTHON = "python3";

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
}
