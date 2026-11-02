using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Json;

namespace Credfeto.Build.Core.Overrides;

public static class OverridesLoader
{
    public const string RELATIVE_PATH = ".ai/commands.json";

    private const int MAX_FILE_LENGTH = 64 * 1024;

    public static async ValueTask<OverridesLoadResult> LoadAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        FileInfo file = new(Path.Combine(path1: repositoryRoot, path2: RELATIVE_PATH));

        if (!file.Exists)
        {
            return new(Overrides: RepositoryOverrides.None, Error: null);
        }

        if (file.Length > MAX_FILE_LENGTH)
        {
            return Ignored(RELATIVE_PATH + " is larger than 64 KiB and was ignored");
        }

        try
        {
            await using FileStream stream = file.OpenRead();
            RepositoryOverrides? overrides = await JsonSerializer.DeserializeAsync(
                utf8Json: stream,
                jsonTypeInfo: CoreJsonContext.Default.RepositoryOverrides,
                cancellationToken: cancellationToken
            );

            return overrides is null
                ? Ignored(RELATIVE_PATH + " is empty and was ignored")
                : new(Overrides: overrides, Error: null);
        }
        catch (JsonException exception)
        {
            // Fixed text plus the position only, so the file's content is never echoed back
            string line = (exception.LineNumber + 1)?.ToString(CultureInfo.InvariantCulture) ?? "?";

            return Ignored(RELATIVE_PATH + " is not valid (line " + line + ") and was ignored");
        }
        catch (IOException)
        {
            return Ignored(RELATIVE_PATH + " could not be read and was ignored");
        }
    }

    private static OverridesLoadResult Ignored(string error)
    {
        return new(Overrides: RepositoryOverrides.None, Error: error);
    }
}
