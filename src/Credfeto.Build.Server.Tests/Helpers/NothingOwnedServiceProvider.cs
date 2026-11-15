using System;

namespace Credfeto.Build.Server.Tests.Helpers;

// A provider that owns nothing, so there is nothing to dispose
internal sealed class NothingOwnedServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        return null;
    }
}
