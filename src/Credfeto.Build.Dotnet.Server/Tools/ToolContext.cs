using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Jobs;
using Credfeto.Build.Dotnet.Server.Commands;
using Credfeto.Build.Dotnet.Server.Detection;
using Credfeto.Build.Dotnet.Server.Quality;
using Credfeto.Build.Dotnet.Server.Tools.Exceptions;

namespace Credfeto.Build.Dotnet.Server.Tools;

public sealed class ToolContext
{
    public ToolContext(
        IJobRunner runner,
        ICommandRunner commands,
        IDotnetDetectionSource detection,
        BuildtestLocator buildtest
    )
    {
        this.Runner = runner;
        this.Commands = commands;
        this.Detection = detection;
        this.Buildtest = buildtest;
    }

    public IJobRunner Runner { get; }

    public ICommandRunner Commands { get; }

    public IDotnetDetectionSource Detection { get; }

    public BuildtestLocator Buildtest { get; }

    public async ValueTask<DotnetRepository> CurrentRepositoryAsync(CancellationToken cancellationToken)
    {
        DotnetDetection detection = await this.Detection.RefreshAsync(cancellationToken);

        return detection.Repository
            ?? throw new ToolUnavailableException(
                "There is no .NET solution in the repository any more (" + detection.Reason + ")"
            );
    }
}
