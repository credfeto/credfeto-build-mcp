namespace Credfeto.Build.Node.Server.Detection;

// The package.json sections a dependency can be declared in that update_package keeps it in
public enum DependencySection
{
    Dependencies,
    DevDependencies,
    OptionalDependencies,
}
