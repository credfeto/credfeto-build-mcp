using System.Text.RegularExpressions;
using Credfeto.Build.Server.Tools;

namespace Credfeto.Build.Node.Server.Tools;

// The args parameter of build and lint: values handed to the script after --, each as its own argument. npm is never
// run through a shell, so the pattern only keeps them to plain tokens: names, flags, key=value pairs and paths
public static partial class ScriptArguments
{
    public const string NAME = "args";

    public const string PATTERN = "^[A-Za-z0-9_.@+=:,/-]{1,256}$";

    public static SchemaProperty Property { get; } =
        new(
            Name: NAME,
            Description: "Arguments handed to the script after --, each as its own argument, for scripts that need to be told what to do (for example which target to build). Each may hold only letters, digits and _ . @ + = : , / - and be at most 256 characters long",
            Values: null,
            Pattern: PATTERN,
            DefaultValue: null,
            Required: false
        )
        {
            StringArray = true,
        };

    [GeneratedRegex(pattern: PATTERN, options: RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Argument();
}
