using System;
using System.Collections.Generic;
using Credfeto.Build.Core.Resources;
using Credfeto.Build.Testing;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Build.Core.Tests.Resources;

public sealed class MemoryLimitTests : TestBase
{
    private const long FOUR_GIB = 4L * 1024 * 1024 * 1024;

    private const string MEMINFO =
        "MemTotal:       16384000 kB\nMemFree:         1024000 kB\nMemAvailable:    8192000 kB\nBuffers:          102400 kB\n";

    [Fact]
    public void ACgroupV2LimitIsRead()
    {
        MemoryLimit? limit = MemoryLimit.Detect(FixedMemorySource.CgroupV2(FOUR_GIB));

        Assert.Equal(expected: new MemoryLimit(Bytes: FOUR_GIB, Source: "cgroup v2"), actual: limit);
    }

    [Fact]
    public void ACgroupV2LimitWinsOverTheOthers()
    {
        MemoryLimit? limit = MemoryLimit.Detect(
            Files(
                (MemoryLimit.CGROUP_V2_PATH, "4294967296\n"),
                (MemoryLimit.CGROUP_V1_PATH, "1073741824\n"),
                (MemoryLimit.MEMINFO_PATH, MEMINFO)
            )
        );

        Assert.Equal(expected: new MemoryLimit(Bytes: FOUR_GIB, Source: "cgroup v2"), actual: limit);
    }

    [Fact]
    public void AnUnlimitedCgroupV2FallsThroughToCgroupV1()
    {
        MemoryLimit? limit = MemoryLimit.Detect(
            Files((MemoryLimit.CGROUP_V2_PATH, "max\n"), (MemoryLimit.CGROUP_V1_PATH, "4294967296\n"))
        );

        Assert.Equal(expected: new MemoryLimit(Bytes: FOUR_GIB, Source: "cgroup v1"), actual: limit);
    }

    [Fact]
    public void AnUnlimitedCgroupV2FallsThroughToMemAvailable()
    {
        MemoryLimit? limit = MemoryLimit.Detect(
            Files((MemoryLimit.CGROUP_V2_PATH, "max\n"), (MemoryLimit.MEMINFO_PATH, MEMINFO))
        );

        Assert.Equal(expected: new MemoryLimit(Bytes: 8192000L * 1024, Source: "MemAvailable"), actual: limit);
    }

    [Fact]
    public void TheCgroupV1NoLimitValueFallsThroughToMemAvailable()
    {
        MemoryLimit? limit = MemoryLimit.Detect(
            Files((MemoryLimit.CGROUP_V1_PATH, "9223372036854771712\n"), (MemoryLimit.MEMINFO_PATH, MEMINFO))
        );

        Assert.Equal(expected: new MemoryLimit(Bytes: 8192000L * 1024, Source: "MemAvailable"), actual: limit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("4 GB")]
    [InlineData("not a number")]
    [InlineData("99999999999999999999999")]
    public static void AnUnreadableCgroupLimitIsIgnored(string content)
    {
        MemoryLimit? limit = MemoryLimit.Detect(
            Files((MemoryLimit.CGROUP_V2_PATH, content), (MemoryLimit.CGROUP_V1_PATH, content))
        );

        Assert.Null(limit);
    }

    [Fact]
    public void MemInfoWithoutMemAvailableIsNoLimit()
    {
        MemoryLimit? limit = MemoryLimit.Detect(
            Files((MemoryLimit.MEMINFO_PATH, "MemTotal:       16384000 kB\nMemFree:         1024000 kB\n"))
        );

        Assert.Null(limit);
    }

    [Theory]
    [InlineData("MemAvailable:\n")]
    [InlineData("MemAvailable:    0 kB\n")]
    [InlineData("MemAvailable:    lots kB\n")]
    [InlineData("MemAvailable:    99999999999999999 kB\n")]
    public static void AnUnreadableMemAvailableIsNoLimit(string meminfo)
    {
        MemoryLimit? limit = MemoryLimit.Detect(Files((MemoryLimit.MEMINFO_PATH, meminfo)));

        Assert.Null(limit);
    }

    [Fact]
    public void AMemAvailableWithoutAUnitIsRead()
    {
        MemoryLimit? limit = MemoryLimit.Detect(Files((MemoryLimit.MEMINFO_PATH, "MemAvailable: 2048")));

        Assert.Equal(expected: new MemoryLimit(Bytes: 2048L * 1024, Source: "MemAvailable"), actual: limit);
    }

    [Fact]
    public void NothingReadableIsNoLimit()
    {
        Assert.Null(MemoryLimit.Detect(FixedMemorySource.None));
    }

    [Fact]
    public void MegabytesAreWholeMebibytes()
    {
        MemoryLimit limit = new(Bytes: FOUR_GIB + 1024, Source: "cgroup v2");

        Assert.Equal(expected: 4096, actual: limit.Megabytes);
    }

    private static FixedMemorySource Files(params (string Path, string Content)[] files)
    {
        Dictionary<string, string> contents = [with(StringComparer.Ordinal)];

        foreach ((string path, string content) in files)
        {
            contents[path] = content;
        }

        return new(contents);
    }
}
