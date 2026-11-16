using System;
using Credfeto.Build.Core.Repository;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Commands;

// Finds npm on PATH as an absolute path, so a job never runs whatever a relative PATH entry or the working
// directory happens to hold
public sealed class NpmLocator
{
    public const string NPM = "npm";

    private readonly Func<string, string?> _find;

    public NpmLocator(Func<string, string?> find)
    {
        this._find = find;
    }

    public static NpmLocator FromProcess()
    {
        return new(static name =>
            ExecutableSearch.Find(name: name, searchPath: Environment.GetEnvironmentVariable("PATH"))
        );
    }

    public string Require()
    {
        return this._find(NPM) ?? throw new ToolUnavailableException("npm was not found on PATH");
    }
}
