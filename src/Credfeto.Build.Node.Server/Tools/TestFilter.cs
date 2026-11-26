using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Credfeto.Build.Node.Server.Commands;
using Credfeto.Build.Node.Server.Detection;

namespace Credfeto.Build.Node.Server.Tools;

// Narrows a test run to some files and asks its runner for coverage. npm runs a script through a shell, so a path
// is limited to characters no shell reads specially apart from *, which is allowed so a path can be a glob; it may
// not start with - (an option) or / (rooted), nor have a .. segment that climbs out of the directory the script
// runs in, so whatever a glob matches is still under that directory
public static partial class TestFilter
{
    public const string PATH_PATTERN = "^(?![-/])(?!(?:.*/)?\\.\\.(?:/|$))[A-Za-z0-9_.@+*/-]+$";

    private const string JEST_VITEST_COVERAGE = "--coverage";
    private const string NODE_TEST_COVERAGE = "--experimental-test-coverage";
    private const string PATHS = "the paths were passed to its test script after -- and may have been ignored";
    private const string NO_COVERAGE = "no coverage switch was passed";

    public static ScriptRun Apply(ScriptRun run, IReadOnlyList<string> paths, bool coverage)
    {
        if (paths.Count == 0 && !coverage)
        {
            return run;
        }

        // The paths replace the package's directory filter, since a runner given both runs everything under either
        return run with
        {
            PathFilter = paths.Count == 0 ? run.PathFilter : null,
            RunnerArguments = [.. CoverageArguments(runner: run.Package.TestRunner, coverage: coverage), .. paths],
        };
    }

    // Only an unknown runner might not take positional paths or a coverage switch, so only it is mentioned
    public static string? Note(IReadOnlyList<ScriptRun> runs, IReadOnlyList<string> paths, bool coverage)
    {
        string[] unknown =
        [
            .. runs.Where(static run => run.Package.TestRunner == NodeTestRunner.Unknown)
                .Select(static run => run.Package.Name)
                .Distinct(StringComparer.Ordinal),
        ];

        if (unknown.Length == 0 || paths.Count == 0 && !coverage)
        {
            return null;
        }

        return "The test runner of "
            + string.Join(separator: ", ", values: unknown)
            + " is not known, so "
            + Consequences(paths: paths, coverage: coverage);
    }

    [GeneratedRegex(pattern: PATH_PATTERN, options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Path();

    private static string Consequences(IReadOnlyList<string> paths, bool coverage)
    {
        if (!coverage)
        {
            return PATHS;
        }

        return paths.Count == 0 ? NO_COVERAGE : PATHS + ", and " + NO_COVERAGE;
    }

    private static IReadOnlyList<string> CoverageArguments(NodeTestRunner runner, bool coverage)
    {
        if (!coverage)
        {
            return [];
        }

        return runner switch
        {
            NodeTestRunner.Jest or NodeTestRunner.Vitest => [JEST_VITEST_COVERAGE],
            NodeTestRunner.NodeTest => [NODE_TEST_COVERAGE],
            _ => [],
        };
    }
}
