using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Build.Python.Server.Detection;

// Python test files (test_*.py or *_test.py) found in a repository that has no manifest: the directories that hold
// them, relative to the repository root, in order
[DebuggerDisplay("TestDirectories={TestDirectories.Count}")]
public sealed record PythonTestSuite(IReadOnlyList<string> TestDirectories);
