using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Build.Core.Repository;

public static class GitTopLevel
{
    private static readonly string[] ShowTopLevel = ["rev-parse", "--show-toplevel"];

    // The top-level folder of the git checkout holding the directory, or null when it is not in one (or git is
    // missing or fails). Through GitCommand, so a GIT_DIR inherited from a hook can't point it at another repository
    public static async ValueTask<string?> FindAsync(string directory, CancellationToken cancellationToken)
    {
        string? output = await GitCommand.TryRunAsync(
            repositoryRoot: directory,
            arguments: ShowTopLevel,
            cancellationToken: cancellationToken
        );

        if (output is null)
        {
            return null;
        }

        // Only the one line break git adds is removed: a folder name may itself end in whitespace
        string topLevel = output is [.., '\n'] ? output[..^1] : output;

        return Path.IsPathFullyQualified(topLevel) ? topLevel : null;
    }
}
