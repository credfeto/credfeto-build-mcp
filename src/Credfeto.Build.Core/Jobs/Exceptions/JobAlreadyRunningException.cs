using System;
using System.ComponentModel;

namespace Credfeto.Build.Core.Jobs.Exceptions;

[Description("Another job is still running; poll job_status until it finishes, then start the next one")]
public sealed partial class JobAlreadyRunningException : Exception;
