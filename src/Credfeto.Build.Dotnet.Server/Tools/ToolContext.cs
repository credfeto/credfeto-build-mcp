using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

/// <summary>What every tool needs: the job runner, a fresh view of the repository and the buildtest script.</summary>
public sealed class ToolContext
{
    public ToolContext(IJobRunner runner, IDotnetDetectionSource detection, BuildtestLocator buildtest)
    {
        this.Runner = runner;
        this.Detection = detection;
        this.Buildtest = buildtest;
    }

    public IJobRunner Runner { get; }

    public IDotnetDetectionSource Detection { get; }

    public BuildtestLocator Buildtest { get; }

    /// <summary>
    ///     The repository as it is now, not as it was when the tool list was published, so every call is checked
    ///     against the projects that exist at the time of the call.
    /// </summary>
    public async ValueTask<DotnetRepository> CurrentRepositoryAsync(CancellationToken cancellationToken)
    {
        DotnetDetection detection = await this.Detection.RefreshAsync(cancellationToken);

        return detection.Repository
            ?? throw new ToolUnavailableException("There is no .NET solution in the repository any more (" + detection.Reason + ")");
    }
}
