using System;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Tests.Helpers;

// A made-up npm workspace: a root package with test and lint scripts, and two workspace packages under packages/
internal static class SampleWorkspace
{
    public const string ROOT_NAME = "example-root";
    public const string WEB = "@example/web";
    public const string CORE = "@example/core";

    public static NodeRepository Create(string repositoryRoot)
    {
        return Create(repositoryRoot: repositoryRoot, rootScripts: [NodeScripts.LINT, NodeScripts.TEST]);
    }

    public static NodeRepository Create(string repositoryRoot, string[] rootScripts)
    {
        return new(
            RepositoryRoot: repositoryRoot,
            InstallRoots: [new NodeInstallRoot(RelativeDirectory: ".", LockFile: "package-lock.json")],
            Packages:
            [
                new NodePackage(Name: ROOT_NAME, RelativeDirectory: ".", Scripts: rootScripts)
                {
                    ScriptNames = rootScripts,
                    TestRunner = NodeTestRunner.Vitest,
                    Dependencies = [new NodeDependency(Name: "vitest", Section: DependencySection.DevDependencies)],
                },
                new NodePackage(
                    Name: WEB,
                    RelativeDirectory: "packages/web",
                    Scripts: [NodeScripts.BUILD, NodeScripts.TEST, NodeScripts.TEST_NO_E2E]
                )
                {
                    ScriptNames = [NodeScripts.BUILD, NodeScripts.TEST, NodeScripts.TEST_NO_E2E, "test:e2e"],
                    TestRunner = NodeTestRunner.Jest,
                    Dependencies =
                    [
                        new NodeDependency(Name: "react", Section: DependencySection.Dependencies),
                        new NodeDependency(Name: "vitest", Section: DependencySection.DevDependencies),
                    ],
                },
                new NodePackage(Name: CORE, RelativeDirectory: "packages/core", Scripts: [NodeScripts.TEST])
                {
                    ScriptNames = [NodeScripts.TEST],
                    TestRunner = NodeTestRunner.NodeTest,
                    Dependencies =
                    [
                        new NodeDependency(Name: "left-pad", Section: DependencySection.OptionalDependencies),
                    ],
                },
            ],
            Timeout: TimeSpan.FromMinutes(30)
        );
    }
}
