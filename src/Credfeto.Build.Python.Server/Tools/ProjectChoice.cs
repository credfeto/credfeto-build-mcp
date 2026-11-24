using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Server.Tools;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Tools;

// The package parameter: all, or one of the supported projects, as an enum built from the repository
internal static class ProjectChoice
{
    public const string NAME = "package";

    public static IReadOnlyList<string> Choices(PythonRepository repository)
    {
        return
        [
            PythonDetector.ALL_PROJECTS,
            .. repository.SupportedProjects.Select(static project => project.Name).Order(StringComparer.Ordinal),
        ];
    }

    public static SchemaProperty Property(PythonRepository repository, string description)
    {
        return new(
            Name: NAME,
            Description: description,
            Values: Choices(repository),
            Pattern: null,
            DefaultValue: PythonDetector.ALL_PROJECTS,
            Required: false
        );
    }

    // The projects to run now, re-read from the repository as it is at the call
    public static IReadOnlyList<PythonProject> Select(
        ToolArguments arguments,
        SchemaProperty property,
        PythonRepository repository
    )
    {
        string choice = arguments.GetChoice(property: property, allowed: Choices(repository))
            ?? PythonDetector.ALL_PROJECTS;
        PythonProject[] projects =
        [
            .. repository.SupportedProjects.Where(project =>
                StringComparer.Ordinal.Equals(x: choice, y: PythonDetector.ALL_PROJECTS)
                || StringComparer.Ordinal.Equals(x: project.Name, y: choice)
            ),
        ];

        return projects.Length != 0
            ? projects
            : throw new ToolUnavailableException("There is no supported Python project any more");
    }
}
