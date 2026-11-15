using System;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Build.Server.Tests.Helpers;

internal static class ValidatingProvider
{
    public static IServiceProvider Build(IServiceCollection services)
    {
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
    }
}
