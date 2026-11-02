using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;

namespace Credfeto.Build.Core.Tests.Helpers;

internal sealed class FixedChangedFilesProbe : IChangedFilesProbe
{
    private readonly IReadOnlyList<string> _files;

    public FixedChangedFilesProbe(params string[] files)
    {
        this._files = files;
    }

    public ValueTask<IReadOnlyList<string>> CollectAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(this._files);
    }
}
