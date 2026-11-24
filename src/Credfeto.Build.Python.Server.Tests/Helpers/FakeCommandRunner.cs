using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Server.Commands;

namespace Credfeto.Build.Python.Server.Tests.Helpers;

// Answers each command by its step name with a canned output and records what was run; nothing is started
internal sealed class FakeCommandRunner : ICommandRunner
{
    private readonly Dictionary<string, CommandOutput> _outputs = [with(StringComparer.Ordinal)];
    private readonly List<ProcessStep> _steps = [];

    public IReadOnlyList<ProcessStep> Steps => this._steps;

    public FakeCommandRunner Answer(string stepName, CommandOutput output)
    {
        this._outputs[stepName] = output;

        return this;
    }

    public FakeCommandRunner Answer(string stepName, string standardOutput)
    {
        return this.Answer(
            stepName: stepName,
            output: new(ExitCode: 0, StandardOutput: standardOutput, TimedOut: false)
        );
    }

    public ValueTask<CommandOutput> RunAsync(ProcessStep step, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this._steps.Add(step);

        return ValueTask.FromResult(
            this._outputs.TryGetValue(key: step.Name, value: out CommandOutput? output)
                ? output
                : new(ExitCode: 1, StandardOutput: string.Empty, TimedOut: false)
        );
    }
}
