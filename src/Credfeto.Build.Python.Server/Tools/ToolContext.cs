using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;
using Credfeto.Build.Server.Tools.Exceptions;

namespace Credfeto.Build.Python.Server.Tools;

public sealed class ToolContext
{
    public ToolContext(IPythonDetectionSource detection, IJobRunner runner, PythonLocator python)
    {
        this.Detection = detection;
        this.Runner = runner;
        this.Python = python;
    }

    public IPythonDetectionSource Detection { get; }

    public IJobRunner Runner { get; }

    public PythonLocator Python { get; }

    public static PythonRepository RepositoryOf(PythonDetection detection)
    {
        return detection.Repository
            ?? throw new ToolUnavailableException("There is no Python project any more (" + detection.Reason + ")");
    }

    // Every tool call detects the repository afresh, so a job never runs against projects that have gone
    public async ValueTask<PythonRepository> CurrentRepositoryAsync(CancellationToken cancellationToken)
    {
        return RepositoryOf(await this.Detection.RefreshAsync(cancellationToken));
    }
}
