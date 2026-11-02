using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Build.Dotnet.Server.Detection;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>Builds the unit test project enums, and checks a chosen project against the repository as it is now.</summary>
public static class UnitTestProjectChoice
{
    public static string[] Names(DotnetRepository repository)
    {
        return [.. repository.UnitTestProjects.Select(static project => project.Name)];
    }

    public static DotnetProject Find(DotnetRepository repository, string name)
    {
        return repository.UnitTestProjects.First(project => StringComparer.Ordinal.Equals(x: project.Name, y: name));
    }

    public static IReadOnlyList<DotnetProject> Select(DotnetRepository repository, string choice, string all)
    {
        return StringComparer.Ordinal.Equals(x: choice, y: all) ? repository.UnitTestProjects : [Find(repository: repository, name: choice)];
    }
}
