using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Node.Server.Detection;
using Credfeto.Build.Node.Server.Json;
using Credfeto.Build.Node.Server.Models;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

public sealed class DetectTool : BuildTool
{
    private const string PACKAGE_MANAGER = "npm";

    private readonly ToolContext _context;

    public DetectTool(ToolContext context)
        : base(
            name: ToolNames.DETECT,
            title: "Detect Node",
            description: "Lists the npm install roots with their lock files, and the packages (workspaces included) with their install root, the build, lint, test, test:noe2e and test:coverage scripts each has, and the test runner its tests use (vitest, jest, node --test or unknown), as they are now (the repository is detected afresh on every call)",
            schema: [],
            readOnly: true
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        IToolProgress progress,
        CancellationToken cancellationToken
    )
    {
        NodeDetection detection = await this._context.Detection.RefreshAsync(cancellationToken);
        NodeRepository repository = ToolContext.RepositoryOf(detection);

        DetectResponse response = new(
            RepositoryRoot: repository.RepositoryRoot,
            PackageManager: PACKAGE_MANAGER,
            InstallRoots:
            [
                .. repository.InstallRoots.Select(static root => new DetectedInstallRoot(
                    Directory: root.RelativeDirectory,
                    LockFile: root.LockFile
                )),
            ],
            Packages:
            [
                .. repository.Packages.Select(static package => new DetectedPackage(
                    Name: package.Name,
                    Directory: package.RelativeDirectory,
                    InstallRoot: package.InstallRoot,
                    Scripts: package.Scripts,
                    TestRunner: RunnerName(package.TestRunner)
                )),
            ],
            TimeoutMinutes: (int)repository.Timeout.TotalMinutes,
            OverridesError: detection.OverridesError
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: NodeJsonContext.Default.DetectResponse);
    }

    private static string RunnerName(NodeTestRunner runner)
    {
        return runner switch
        {
            NodeTestRunner.Vitest => "vitest",
            NodeTestRunner.Jest => "jest",
            NodeTestRunner.NodeTest => "node --test",
            _ => "unknown",
        };
    }
}
