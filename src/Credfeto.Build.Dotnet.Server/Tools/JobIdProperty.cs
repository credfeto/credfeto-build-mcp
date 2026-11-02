namespace Credfeto.Build.Dotnet.Server.Tools;

public static class JobIdProperty
{
    public static SchemaProperty Create()
    {
        return new(
            Name: "id",
            Description: "The job id returned by a tool that starts a job",
            Values: null,
            Pattern: ToolArguments.JOB_ID_PATTERN,
            DefaultValue: null,
            Required: true
        );
    }
}
