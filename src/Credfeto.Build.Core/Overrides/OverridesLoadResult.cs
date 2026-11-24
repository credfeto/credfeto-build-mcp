using System.Diagnostics;

namespace Credfeto.Build.Core.Overrides;

// Error is a problem with the whole file; DotnetError, NodeError and PythonError are problems with that language's
// section only, which leave the other sections in force
[DebuggerDisplay(
    "Overrides={Overrides}, Error={Error}, DotnetError={DotnetError}, NodeError={NodeError}, PythonError={PythonError}"
)]
public sealed record OverridesLoadResult(RepositoryOverrides Overrides, string? Error)
{
    public string? DotnetError { get; init; }

    public string? NodeError { get; init; }

    public string? PythonError { get; init; }

    // Everything that affects the .NET settings: the file's own problems and the .NET section's
    public string? DotnetErrors => Combine(first: this.Error, second: this.DotnetError);

    // Everything that affects the Node settings: the file's own problems and the Node section's
    public string? NodeErrors => Combine(first: this.Error, second: this.NodeError);

    // Everything that affects the Python settings: the file's own problems and the Python section's
    public string? PythonErrors => Combine(first: this.Error, second: this.PythonError);

    private static string? Combine(string? first, string? second)
    {
        if (first is null)
        {
            return second;
        }

        return second is null ? first : first + "; " + second;
    }
}
