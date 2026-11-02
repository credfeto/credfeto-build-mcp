namespace Credfeto.Build.Core.Tests.Coverage;

internal static class CoberturaSamples
{
    // Two classes in one package; the method-level lines repeat class-level lines, as Microsoft's collector writes them
    public const string ONE_PACKAGE = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.6" branch-rate="1" version="1.9" timestamp="1">
          <packages>
            <package name="Example.Core" line-rate="0.6">
              <classes>
                <class name="Example.Core.Alpha" filename="/src/Example.Core/Alpha.cs" line-rate="0.666">
                  <methods>
                    <method name="Run" signature="()">
                      <lines>
                        <line number="10" hits="1" branch="False" />
                        <line number="11" hits="0" branch="False" />
                      </lines>
                    </method>
                  </methods>
                  <lines>
                    <line number="10" hits="1" branch="False" />
                    <line number="11" hits="0" branch="False" />
                    <line number="12" hits="3" branch="False" />
                  </lines>
                </class>
                <class name="Example.Core.Beta" filename="/src/Example.Core/Beta.cs" line-rate="0.5">
                  <lines>
                    <line number="5" hits="2" branch="False" />
                    <line number="6" hits="0" branch="False" />
                  </lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    public const string OTHER_PACKAGE = """
        <coverage>
          <packages>
            <package name="Example.Other">
              <classes>
                <class name="Example.Other.Gamma" filename="/src/Example.Other/Gamma.cs">
                  <lines>
                    <line number="1" hits="1" />
                    <line number="2" hits="1" />
                    <line number="3" hits="1" />
                    <line number="4" hits="0" />
                  </lines>
                </class>
              </classes>
            </package>
            <package name="Example.Core">
              <classes>
                <class name="Example.Core.Alpha" filename="/src/Example.Core/Alpha.cs">
                  <lines>
                    <line number="11" hits="4" />
                  </lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
