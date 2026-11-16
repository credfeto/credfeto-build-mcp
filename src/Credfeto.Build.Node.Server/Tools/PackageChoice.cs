using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Node.Server.Tools;

// The package parameter: all, or one of the packages a tool can run for, as an enum built from the repository
internal static class PackageChoice
{
    public const string NAME = "package";

    public static SchemaProperty Property(IReadOnlyList<string> choices, string description)
    {
        return new(
            Name: NAME,
            Description: description,
            Values: choices,
            Pattern: null,
            DefaultValue: ScriptTargets.ALL,
            Required: false
        );
    }

    // The packages and scripts to run now, re-read from the repository as it is at the call
    public static IReadOnlyList<(NodePackage Package, string Script)> Targets(
        ToolArguments arguments,
        SchemaProperty property,
        NodeRepository repository,
        Func<NodePackage, string?> script
    )
    {
        string choice = arguments.GetChoice(
            property: property,
            allowed: ScriptTargets.Choices(repository: repository, script: script)
        ) ?? ScriptTargets.ALL;
        IReadOnlyList<(NodePackage Package, string Script)> targets = ScriptTargets.Select(
            repository: repository,
            choice: choice,
            script: script
        );

        return targets.Count != 0
            ? targets
            : throw new ToolUnavailableException("No package has the script this tool runs any more");
    }

    public static NodeInstallRoot InstallRootOf(NodeRepository repository, string installRoot)
    {
        return repository.InstallRoots.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(x: candidate.RelativeDirectory, y: installRoot)
        ) ?? throw new ToolUnavailableException("The package's install root is no longer detected");
    }
}
