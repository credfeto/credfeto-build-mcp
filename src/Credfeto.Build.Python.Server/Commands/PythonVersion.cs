using System;
using System.Diagnostics;
using System.Globalization;

namespace Credfeto.Build.Python.Server.Commands;

[DebuggerDisplay("{Major}.{Minor}.{Patch}")]
public sealed record PythonVersion(int Major, int Minor, int Patch)
{
    public string MajorMinor =>
        this.Major.ToString(CultureInfo.InvariantCulture) + "." + this.Minor.ToString(CultureInfo.InvariantCulture);

    public string Text => this.MajorMinor + "." + this.Patch.ToString(CultureInfo.InvariantCulture);

    // "3.13.1" as the interpreter prints it; null for anything else
    public static PythonVersion? Parse(string text)
    {
        string[] parts = text.Trim().Split('.');

        return
            parts.Length == 3
            && Number(parts[0]) is { } major
            && Number(parts[1]) is { } minor
            && Number(parts[2]) is { } patch
            ? new(Major: major, Minor: minor, Patch: patch)
            : null;
    }

    private static int? Number(string text)
    {
        return
            text.Length is > 0 and < 6
            && int.TryParse(
                s: text,
                style: NumberStyles.None,
                provider: CultureInfo.InvariantCulture,
                result: out int value
            )
            ? value
            : null;
    }

    public int[] Release()
    {
        return [this.Major, this.Minor, this.Patch];
    }

    public static int Compare(ReadOnlySpan<int> left, ReadOnlySpan<int> right)
    {
        int length = Math.Max(val1: left.Length, val2: right.Length);

        for (int index = 0; index < length; index++)
        {
            int difference = (index < left.Length ? left[index] : 0).CompareTo(index < right.Length ? right[index] : 0);

            if (difference != 0)
            {
                return difference;
            }
        }

        return 0;
    }
}
