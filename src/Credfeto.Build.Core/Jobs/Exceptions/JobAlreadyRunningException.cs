using System;
using System.ComponentModel;

namespace Credfeto.Build.Core.Jobs.Exceptions;

[Description("Another job is still running; wait for it with job_result, then start the next one")]
public sealed partial class JobAlreadyRunningException : Exception;
