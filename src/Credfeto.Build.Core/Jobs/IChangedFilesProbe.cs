using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Jobs;

/// <summary>Reports, once a job's steps have run, which files the job changed.</summary>
public interface IChangedFilesProbe
{
    ValueTask<IReadOnlyList<string>> CollectAsync(CancellationToken cancellationToken);
}
