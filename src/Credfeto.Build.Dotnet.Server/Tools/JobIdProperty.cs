namespace Credfeto.Build.Dotnet.Server.Tools;

public static class JobIdProperty
{
    public static SchemaProperty Create()
    {
        return new(
            Name: "id",
            Description: "The job id returned by restore, build, test or coverage",
            Values: null,
            Pattern: ToolArguments.JOB_ID_PATTERN,
            DefaultValue: null,
            Required: true
        );
    }
}
