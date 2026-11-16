using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Build.Core.Json;

namespace Credfeto.Build.Core.Overrides;

public static class OverridesLoader
{
    public const string RELATIVE_PATH = ".ai/commands.json";

    private const int MAX_FILE_LENGTH = 64 * 1024;

    private const string DOTNET_SECTION = "dotnet";
    private const string NODE_SECTION = "node";

    // Read by the Python server once it exists; a repository may already carry it
    private const string PYTHON_SECTION = "python";

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static async ValueTask<OverridesLoadResult> LoadAsync(
        string repositoryRoot,
        CancellationToken cancellationToken
    )
    {
        FileInfo file = new(Path.Combine(path1: repositoryRoot, path2: RELATIVE_PATH));

        if (!file.Exists)
        {
            return new(Overrides: RepositoryOverrides.None, Error: null);
        }

        if (file.Length > MAX_FILE_LENGTH)
        {
            return TooLarge();
        }

        byte[] content;

        try
        {
            content = await File.ReadAllBytesAsync(path: file.FullName, cancellationToken: cancellationToken);
        }
        catch (IOException)
        {
            return Ignored(RELATIVE_PATH + " could not be read and was ignored");
        }

        // The file can grow between the length check and the read
        return content.Length > MAX_FILE_LENGTH ? TooLarge() : Parse(content);
    }

    private static OverridesLoadResult Parse(ReadOnlySpan<byte> content)
    {
        Utf8JsonReader reader = new(jsonData: content, options: ReaderOptions);

        try
        {
            return ReadSections(ref reader);
        }
        catch (JsonException exception)
        {
            // Fixed text plus the position only, so the file's content is never echoed back
            return Ignored(RELATIVE_PATH + " is not valid (line " + Line(exception) + ") and was ignored");
        }
    }

    // Each language's section is read on its own, so a key one server does not know (or a mistake in its section)
    // only costs that language its settings
    private static OverridesLoadResult ReadSections(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType == JsonTokenType.Null)
        {
            return Ignored(RELATIVE_PATH + " is empty and was ignored");
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return Ignored(RELATIVE_PATH + " is not a JSON object and was ignored");
        }

        SectionsRead sections = new();

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string? name = reader.GetString();

            if (!reader.Read())
            {
                throw new JsonException("The overrides file ended inside a setting");
            }

            ReadSetting(reader: ref reader, name: name, sections: sections);
        }

        if (reader.Read())
        {
            throw new JsonException("The overrides file has content after its object");
        }

        return sections.ToResult();
    }

    private static void ReadSetting(ref Utf8JsonReader reader, string? name, SectionsRead sections)
    {
        switch (name)
        {
            case DOTNET_SECTION:
                (sections.Dotnet, sections.DotnetError) = ReadSection(
                    reader: ref reader,
                    typeInfo: CoreJsonContext.Default.DotnetOverrides,
                    name: DOTNET_SECTION
                );

                break;
            case NODE_SECTION:
                (sections.Node, sections.NodeError) = ReadSection(
                    reader: ref reader,
                    typeInfo: CoreJsonContext.Default.NodeOverrides,
                    name: NODE_SECTION
                );

                break;
            case PYTHON_SECTION:
                reader.Skip();

                break;
            default:
                sections.UnknownSetting = true;
                reader.Skip();

                break;
        }
    }

    private static (T? Value, string? Error) ReadSection<T>(
        ref Utf8JsonReader reader,
        JsonTypeInfo<T> typeInfo,
        string name
    )
        where T : class
    {
        Utf8JsonReader section = reader;

        // Moves past the whole section first, so a section that is not valid for its type still leaves the reader on
        // the next setting; JSON that is not well formed throws here and voids the whole file
        reader.Skip();

        if (section.TokenType == JsonTokenType.Null)
        {
            return (null, null);
        }

        try
        {
            return (JsonSerializer.Deserialize(reader: ref section, jsonTypeInfo: typeInfo), null);
        }
        catch (JsonException exception)
        {
            return (
                null,
                RELATIVE_PATH + ": the " + name + " section is not valid (line " + Line(exception) + ") and was ignored"
            );
        }
    }

    private static string Line(JsonException exception)
    {
        return (exception.LineNumber + 1)?.ToString(CultureInfo.InvariantCulture) ?? "?";
    }

    private static OverridesLoadResult TooLarge()
    {
        return Ignored(RELATIVE_PATH + " is larger than 64 KiB and was ignored");
    }

    private static OverridesLoadResult Ignored(string error)
    {
        return new(Overrides: RepositoryOverrides.None, Error: error);
    }

    private sealed class SectionsRead
    {
        public DotnetOverrides? Dotnet { get; set; }

        public string? DotnetError { get; set; }

        public NodeOverrides? Node { get; set; }

        public string? NodeError { get; set; }

        public bool UnknownSetting { get; set; }

        public OverridesLoadResult ToResult()
        {
            RepositoryOverrides overrides =
                this.Dotnet is null && this.Node is null
                    ? RepositoryOverrides.None
                    : new(Dotnet: this.Dotnet, Node: this.Node);

            return new(
                Overrides: overrides,
                Error: this.UnknownSetting
                    ? RELATIVE_PATH + " has a setting that is not recognised, which was ignored"
                    : null
            )
            {
                DotnetError = this.DotnetError,
                NodeError = this.NodeError,
            };
        }
    }
}
