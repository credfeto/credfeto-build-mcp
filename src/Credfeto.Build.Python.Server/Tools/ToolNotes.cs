using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Credfeto.Build.Python.Server.Commands;
using Credfeto.Build.Python.Server.Detection;

namespace Credfeto.Build.Python.Server.Tools;

// What a job tool tells the caller besides the job: a manifest it created, and test tools it installed because the
// project does not declare them. Added to the job status as "notes"
internal static class ToolNotes
{
    public const string NOTES_PROPERTY = "notes";

    public static IReadOnlyList<string> UndeclaredTools(IEnumerable<PythonProject> projects)
    {
        return
        [
            .. projects
                .Select(static project => (project.Name, Missing: PythonCommands.MissingTools(project)))
                .Where(static entry => entry.Missing.Count != 0)
                .Select(static entry =>
                    "Project "
                    + entry.Name
                    + " does not declare "
                    + string.Join(separator: " or ", values: entry.Missing)
                    + ", so it is installed unpinned into the project's .venv. Declare pytest and coverage in a test extra, dependency group or requirements file, so every run and CI use the same versions."
                ),
        ];
    }

    public static string Add(string status, IReadOnlyList<string> notes)
    {
        if (notes.Count == 0 || JsonNode.Parse(status) is not JsonObject json)
        {
            return status;
        }

        json[NOTES_PROPERTY] = new JsonArray([.. notes.Select(static note => (JsonNode?)JsonValue.Create(note))]);

        return json.ToJsonString();
    }
}
