using System;
using System.ComponentModel;

namespace Credfeto.Build.Core.Configuration.Exceptions;

[Description("The server's environment configuration is not valid")]
public sealed partial class InvalidBuildConfigurationException : Exception;
