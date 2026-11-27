namespace Credfeto.Build.Core.Resources;

public interface IMemorySource
{
    // The file's text, or null when it does not exist or cannot be read
    string? ReadFile(string path);
}
