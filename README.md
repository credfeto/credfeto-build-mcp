# credfeto-build-mcp

MCP servers that build, test and measure coverage for a repository, so an agent never composes a build command
itself. There is one server per language, all sharing one core library. Each server is a trimmed Native AOT binary
that speaks MCP over stdio.

## Build Status

| Branch  | Status                                                |
|---------|-------------------------------------------------------|
| main    | [![Build: Pre-Release][pre-release-img]][pre-release] |
| release | [![Build: Release][release-img]][release]             |

## Projects

| Project | What it is |
|---------|------------|
| `Credfeto.Build.Core` | The shared core (AOT-compatible library): the job runner, repository helpers, Cobertura parsing and the `.ai/commands.json` overrides. |
| `Credfeto.Build.Dotnet.Server` | The `.NET` server (`credfeto-build-dotnet`), a Native AOT executable. |
| `*.Tests` | Unit tests for each of the above. |

## The .NET server

### Detection

At start-up the server looks for a solution in `<repo>/src/` and then in `<repo>/`, taking a `.slnx` in preference
to a `.sln`. It reads the solution's C# projects and sorts them by name:

- `*.Tests` are unit test projects, unless the project sets `IsTestProject` or `IsTestingPlatformApplication` to
  `false`;
- `*.Integration.Tests` and `*.Benchmark.Tests` are listed, but the test and coverage tools never run them.

If no solution is found, the server **registers no tools**: `tools/list` returns an empty list and the server's
instructions say why. A repository with a solution but no unit test projects gets every tool except `test` and
`coverage`.

Detection runs once, before the server answers its first request. The result holds for the whole session, so a
project added later needs a server restart.

### Repository root

The repository the server works on is **never** a tool argument. It is `BUILD_MCP_REPO_ROOT` if that is set to an
absolute path, and otherwise `/workspace/repo`. Every command runs with a fixed working directory under that root,
so the agent never composes a `cd`.

| Variable | Default | Meaning |
|----------|---------|---------|
| `BUILD_MCP_REPO_ROOT` | `/workspace/repo` | The repository to work on. Relative paths are ignored. |
| `BUILD_MCP_LOG_DIR` | `$TMPDIR/credfeto-build-mcp` | Where each job writes its full log (`<job id>.log`, mode 0600). |

### Tools

Every parameter is an enum or a pattern-checked job id. No tool takes a free-form argument string, and a value
outside the enum is refused without anything being run.

| Tool | Parameters | What it does |
|------|------------|--------------|
| `detect` | none | The solution, its projects, the unit, integration and benchmark test projects, the timeout and any overrides error. |
| `restore` | none | Starts `dotnet restore <solution>`. |
| `build` | none | Starts `dotnet build <solution> -c Release`. |
| `test` | `scope`: `all` (default) or `project`; `project`: one of the unit test projects | Starts `dotnet test <Project>/<Project>.csproj -c Release -p:SolutionDir=<src>/` for each chosen project, one at a time. |
| `coverage` | `project`: `all` (default) or one of the unit test projects | Starts the coverage run below for each chosen project, one at a time. |
| `job_status` | `id` | Whether the job is still running, how long it has run and which step it is on. |
| `job_result` | `id` | The outcome: state, each step's exit code, test counts, failing test names, coverage, the masked last 40 lines of output, and the log path. |

`restore`, `build`, `test` and `coverage` start a **job** and return its id at once, so no MCP call or Bash timeout
applies to a long build. Poll `job_status` until `state` is no longer `running`, then read `job_result`. One job runs
at a time; starting another while one is running is refused. A job is killed when it exceeds its timeout (30
minutes by default), and when the server shuts down.

Each step runs as a fixed argument vector, never through a shell. Output is stripped of ANSI codes and masked before
it is logged or returned: the values of environment variables whose names look secret (`*TOKEN*`, `*SECRET*`,
`*PASSWORD*`, `*API_KEY*` and similar), GitHub tokens, `Bearer`/`Basic` credentials, `password=`-style pairs and
credentials in URLs. Lines are capped at 4,096 characters and each log at 16 MiB.

### Coverage

Coverage uses the documented Microsoft.Testing.Platform form, run from the solution's directory with a relative
project path:

```bash
dotnet test <Assembly>.Tests/<Assembly>.Tests.csproj -c Release -p:SolutionDir=<src>/ \
  -- --coverage --coverage-output-format cobertura \
     --coverage-output <repo>/coverage/<Assembly>.coverage.cobertura.xml
```

Stale files for the chosen projects are deleted before the run. When the run ends, the server parses the Cobertura
files **in-process** and returns per-assembly and Overall line coverage, the head SHA, and the same figures as a
`COVERAGE.md`-style table.

The files are parsed in-process rather than with `reportgenerator`, so the server needs no tool from the
repository's tool manifest and works the same in every repository. A line counts once per assembly, and as covered if
any report hit it. Each test project's coverage is already limited
to its own assembly by `UnitTests.props`, so an assembly is measured only by its own tests.

### Per-repository overrides

A repository can narrow what the server does with `.ai/commands.json`:

```json
{
  "dotnet": {
    "enabled": true,
    "solution": "src/Example.slnx",
    "excludeTestProjects": ["Example.Slow.Tests"],
    "timeoutMinutes": 45
  }
}
```

The file can only select among what detection finds, or narrow it. It can never add a command, an argument or an
executable. `enabled: false` turns the server off for the repository. `solution` must be a `.slnx` or `.sln` inside
the repository. `timeoutMinutes` is clamped to 1-240. Unknown properties, invalid JSON or a file over 64 KiB make the
server ignore the whole file, and `detect` reports why.

### Publishing and start-up time

The server is published as a trimmed Native AOT binary, and the publish fails on any trim or AOT warning:

```bash
cd src
dotnet publish Credfeto.Build.Dotnet.Server/Credfeto.Build.Dotnet.Server.csproj -c Release -r linux-x64
```

The binary needs no .NET runtime to start. From process start to the `tools/list` reply, detection included, it must
take under 200 ms. `StartupTimeTests` measures this against the published binary: set `BUILD_MCP_SERVER_BINARY` to
its path and run the server tests. Without the variable, those tests are skipped.

### Registering it (oneshot)

The orchestrator's managed MCP config is fixed in the image, so every language server is always registered and each
decides for itself whether it applies. The image will install the published binary and register it as a stdio
server, for example:

```json
{
  "mcpServers": {
    "build-dotnet": {
      "type": "stdio",
      "command": "/usr/local/bin/credfeto-build-dotnet",
      "env": { "BUILD_MCP_REPO_ROOT": "/workspace/repo" }
    }
  }
}
```

### Follow-ups

Not in this first version:

- `lint` and `format` tools (`dotnet buildcheck` and `dotnet cscleanup`);
- the Node and Python servers on the same core;
- the orchestrator side: installing the binary in the image, registering it in the managed MCP config, allowing its
  tools in managed settings, and then denying raw `dotnet build`, `dotnet test`, `dotnet exec` and
  `dotnet reportgenerator` in Bash;
- a CI step that publishes the server with AOT and runs `StartupTimeTests` against the binary.

## Changelog

View [changelog][changelog].

## Contributing

See [contributing guidelines][contributing].

## Security

See [security policy][security].

## Licence

This project is licensed under the [MIT Licence][licence].

[changelog]: CHANGELOG.md
[contributing]: CONTRIBUTING.md
[licence]: LICENSE
[pre-release-img]: https://github.com/credfeto/credfeto-build-mcp/actions/workflows/build-and-publish-pre-release.yml/badge.svg
[pre-release]: https://github.com/credfeto/credfeto-build-mcp/actions/workflows/build-and-publish-pre-release.yml
[release-img]: https://github.com/credfeto/credfeto-build-mcp/actions/workflows/build-and-publish-release.yml/badge.svg
[release]: https://github.com/credfeto/credfeto-build-mcp/actions/workflows/build-and-publish-release.yml
[security]: SECURITY.md
