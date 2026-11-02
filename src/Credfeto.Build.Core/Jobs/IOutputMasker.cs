namespace Credfeto.Build.Core.Jobs;

public interface IOutputMasker
{
    string Mask(string line);
}
