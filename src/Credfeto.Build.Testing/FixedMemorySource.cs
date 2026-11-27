using System;
using System.Collections.Generic;
using System.Globalization;
using Credfeto.Build.Core.Resources;

namespace Credfeto.Build.Testing;

// Answers reads of the memory files from a fixed set, so a test chooses exactly which limits the host appears to have
public sealed class FixedMemorySource : IMemorySource
{
    private readonly IReadOnlyDictionary<string, string> _files;

    public FixedMemorySource(IReadOnlyDictionary<string, string> files)
    {
        this._files = files;
    }

    public static FixedMemorySource None { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    public static FixedMemorySource CgroupV2(long bytes)
    {
        return new(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MemoryLimit.CGROUP_V2_PATH] = bytes.ToString(CultureInfo.InvariantCulture) + "\n",
            }
        );
    }

    public string? ReadFile(string path)
    {
        return this._files.GetValueOrDefault(path);
    }
}
