using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Server.Tests.Helpers;

// Hands the parsed arguments to the test, and answers with whatever it returns
internal sealed class EchoTool : BuildTool
{
    private readonly Func<ToolArguments, string> _execute;

    public EchoTool(IReadOnlyList<SchemaProperty> schema, Func<ToolArguments, string> execute)
        : base(
            name: "echo",
            title: "Echo",
            description: "Answers with the parsed arguments",
            schema: schema,
            readOnly: false
        )
    {
        this._execute = execute;
    }

    protected override ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(this._execute(arguments));
    }
}
