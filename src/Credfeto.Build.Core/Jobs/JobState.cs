using System.Text.Json.Serialization;

namespace Credfeto.Build.Core.Jobs;

public enum JobState
{
    [JsonStringEnumMemberName("running")]
    Running,

    [JsonStringEnumMemberName("succeeded")]
    Succeeded,

    [JsonStringEnumMemberName("failed")]
    Failed,

    [JsonStringEnumMemberName("timed-out")]
    TimedOut,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
