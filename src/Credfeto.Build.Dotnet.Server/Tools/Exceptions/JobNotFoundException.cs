using System;
using System.ComponentModel;

namespace Credfeto.Build.Dotnet.Server.Tools.Exceptions;

[Description("No job with that id is known to this server; ids do not survive a server restart")]
public sealed partial class JobNotFoundException : Exception;
