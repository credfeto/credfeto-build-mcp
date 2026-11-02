using System;
using System.ComponentModel;

namespace Credfeto.Build.Core.Coverage.Exceptions;

[Description("The Cobertura coverage report could not be read")]
public sealed partial class InvalidCoberturaException : Exception;
