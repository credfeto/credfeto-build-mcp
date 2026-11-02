using System;
using System.ComponentModel;

namespace Credfeto.Build.Dotnet.Server.Tools.Exceptions;

[Description("The tool cannot run in this repository as it is now")]
public sealed partial class ToolUnavailableException : Exception;
