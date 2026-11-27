using System;
using System.Diagnostics;
using System.Globalization;

namespace Credfeto.Build.Core.Resources;

[DebuggerDisplay("Bytes={Bytes}, Source={Source}")]
public sealed record MemoryLimit(long Bytes, string Source)
{
    public const string CGROUP_V2_PATH = "/sys/fs/cgroup/memory.max";
    public const string CGROUP_V1_PATH = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
    public const string MEMINFO_PATH = "/proc/meminfo";

    private const string MEM_AVAILABLE = "MemAvailable:";
    private const long BYTES_PER_KIB = 1024;
    private const long BYTES_PER_MIB = 1024 * 1024;

    // cgroup v1 reports an unset limit as a page-aligned value near long.MaxValue rather than a word, so anything this
    // large is no limit at all
    private const long CGROUP_V1_UNLIMITED = 1L << 60;

    public long Megabytes => this.Bytes / BYTES_PER_MIB;

    // The container's limit wins over what the host has free, because the kernel kills the job at the cgroup limit;
    // with no limit set, what is available now is the most the job can use. Null when neither can be read
    public static MemoryLimit? Detect(IMemorySource source)
    {
        if (CgroupLimit(text: source.ReadFile(CGROUP_V2_PATH), unlimitedFrom: long.MaxValue) is { } v2)
        {
            return new(Bytes: v2, Source: "cgroup v2");
        }

        if (CgroupLimit(text: source.ReadFile(CGROUP_V1_PATH), unlimitedFrom: CGROUP_V1_UNLIMITED) is { } v1)
        {
            return new(Bytes: v1, Source: "cgroup v1");
        }

        return Available(source.ReadFile(MEMINFO_PATH)) is { } available
            ? new(Bytes: available, Source: "MemAvailable")
            : null;
    }

    // cgroup v2 writes "max" for no limit, which fails to parse and so reads as none
    private static long? CgroupLimit(string? text, long unlimitedFrom)
    {
        return
            text is not null
            && long.TryParse(
                s: text.Trim(),
                style: NumberStyles.None,
                provider: CultureInfo.InvariantCulture,
                result: out long bytes
            )
            && bytes > 0
            && bytes < unlimitedFrom
            ? bytes
            : null;
    }

    private static long? Available(string? meminfo)
    {
        if (meminfo is null)
        {
            return null;
        }

        foreach (string line in meminfo.Split('\n'))
        {
            if (!line.StartsWith(value: MEM_AVAILABLE, comparisonType: StringComparison.Ordinal))
            {
                continue;
            }

            return ParseKibibytes(line[MEM_AVAILABLE.Length..]);
        }

        return null;
    }

    // The value is written as "<number> kB", where kB means KiB
    private static long? ParseKibibytes(string value)
    {
        string trimmed = value.Trim();
        int space = trimmed.IndexOf(value: ' ', comparisonType: StringComparison.Ordinal);
        string number = space >= 0 ? trimmed[..space] : trimmed;

        return
            long.TryParse(
                s: number,
                style: NumberStyles.None,
                provider: CultureInfo.InvariantCulture,
                result: out long kibibytes
            )
            && kibibytes > 0
            && kibibytes < long.MaxValue / BYTES_PER_KIB
            ? kibibytes * BYTES_PER_KIB
            : null;
    }
}
