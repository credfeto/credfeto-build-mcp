using System;
using System.Collections.Generic;
using System.Linq;

namespace Credfeto.Build.Node.Server.Detection;

// Infers the test runner from the test script's command, then from the declared dependencies
public static class TestRunnerInference
{
    private const string VITEST = "vitest";
    private const string JEST = "jest";
    private const string NODE = "node";
    private const string NODE_TEST_SWITCH = "--test";

    private static readonly char[] Whitespace = [' ', '\t'];

    public static NodeTestRunner Infer(string? testCommand, IReadOnlyList<NodeDependency> dependencies)
    {
        NodeTestRunner fromCommand = FromCommand(testCommand);

        if (fromCommand != NodeTestRunner.Unknown)
        {
            return fromCommand;
        }

        if (Declares(dependencies: dependencies, name: VITEST))
        {
            return NodeTestRunner.Vitest;
        }

        return Declares(dependencies: dependencies, name: JEST) ? NodeTestRunner.Jest : NodeTestRunner.Unknown;
    }

    // The first word after any leading VARIABLE=value assignments names the runner
    private static NodeTestRunner FromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return NodeTestRunner.Unknown;
        }

        string[] words =
        [
            .. command
                .Split(separator: Whitespace, options: StringSplitOptions.RemoveEmptyEntries)
                .SkipWhile(static word => word.Contains('=', StringComparison.Ordinal)),
        ];

        if (words.Length == 0)
        {
            return NodeTestRunner.Unknown;
        }

        return words[0] switch
        {
            VITEST => NodeTestRunner.Vitest,
            JEST => NodeTestRunner.Jest,
            NODE when words.Contains(value: NODE_TEST_SWITCH, comparer: StringComparer.Ordinal) =>
                NodeTestRunner.NodeTest,
            _ => NodeTestRunner.Unknown,
        };
    }

    private static bool Declares(IReadOnlyList<NodeDependency> dependencies, string name)
    {
        return dependencies.Any(dependency => StringComparer.Ordinal.Equals(x: dependency.Name, y: name));
    }
}
