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
| --- | --- |
| `Credfeto.Build.Core` | The shared core (AOT-compatible library): the job runner, repository helpers, Cobertura parsing and the `.ai/commands.json` overrides. |
| `Credfeto.Build.Dotnet.Server` | The `.NET` server (`credfeto-build-dotnet`), a Native AOT executable. |
| `*.Tests` | Unit tests for each of the above. |

## The .NET server

### Detection

At start-up the server looks for a solution in `<repo>/src/` and then in `<repo>/`, taking a `.slnx` in preference
to a `.sln`. It reads the solution's C# projects and sorts them by name:

- `*.Tests` are unit test projects, unless the project sets `IsTestProject` or `IsTestingPlatformApplication` to
  `false`;
- `*.Integration.Tests` and `*.Benchmark.Tests` are listed and can be run by name with `test` scope `project`. The
  solution-wide `test` run (scope `all`) leaves their namespaces out, as pre-commit `buildtest` does, and `coverage`
  never offers them.

If no solution is found, the server **registers no tools**: `tools/list` returns an empty list and the server's
instructions say why. A repository with a solution but no unit test projects gets every tool except `test` and
`coverage`.

### Keeping up with the repository

The server runs for a whole session while the repository changes underneath it, so the tool list is not frozen at
start-up:

- **Watch.** Once the server is up, a `FileSystemWatcher` watches the repository root recursively for `*.csproj`,
  `*.sln` and `*.slnx` files and `.ai/commands.json` being created, deleted, renamed or edited, and for directories
  coming and going. Anything under `bin/`, `obj/` or `.git/` is ignored. Changes are debounced (500 ms), then
  detection runs again. When the result differs, the server rebuilds the tool list and its `project` enums in one
  change and sends `notifications/tools/list_changed`; it declares `tools.listChanged: true`. The same mechanism turns
  the tools on when a solution first appears and off when it goes.
- **Validate every call.** Each tool call detects the repository afresh and checks its `project` against that, not
  against the enum the client last read. A project added after start-up is accepted even by a client that never
  re-reads the schema, and a deleted one is refused. A call made after the solution has gone fails with a clear
  message.

If the watch cannot start (the repository root is missing, or the system's inotify limits are reached), the server
carries on without it; the per-call check still applies.

### Repository root

The repository the server works on is **never** a tool argument. It is `BUILD_MCP_REPO_ROOT` when that is set, and
`/workspace/repo` when it is unset. A value that is set but is not an absolute path (relative, empty or whitespace)
stops the server at startup (and fails `--self-test`) with exit code 1 and a message on stderr naming the variable
and the value; it is never swapped for the default. `BUILD_MCP_LOG_DIR` follows the same rule. Every command runs with a fixed working directory under that root,
so the agent never composes a `cd`.

| Variable | Default | Meaning |
| --- | --- | --- |
| `BUILD_MCP_REPO_ROOT` | `/workspace/repo` | The repository to work on. Must be an absolute path when set; anything else is a startup error. |
| `BUILD_MCP_LOG_DIR` | `$XDG_STATE_HOME/credfeto-build-mcp/logs`, else `~/.local/state/credfeto-build-mcp/logs`, else `$TMPDIR/credfeto-build-mcp-<uid>` when there is no home directory | Where each job writes its full log (`<job id>.log`, mode 0600) in a per-user folder with mode 0700. Must be an absolute path when set; anything else is a startup error. A relative `XDG_STATE_HOME` or `HOME` is ignored when choosing the default. |
| `BUILD_MCP_BUILDTEST` | `buildtest` on `PATH` | The absolute path of the pre-commit `buildtest` script that the `buildtest` tool runs. When set to anything else (including empty) the `buildtest` tool fails with a message showing the value. |

### Tools

Every parameter is an enum, a pattern-checked id (a job id, or a NuGet package id), a flag or a bounded whole
number. No tool takes a free-form argument string, and a value outside those bounds is refused without anything
being run.

The `buildtest` properties that `build` and `test` pass are `-p:ApiCompatGenerateSuppressionFile=true`,
`-p:ContinuousIntegrationBuild=true`, `-p:IsProduction=false`, `-p:Optimize=true`,
`-p:SuppressNETCoreSdkPreviewMessage=true` and `-p:Version=0.0.0.1-test`.

| Tool | Parameters | What it does |
| --- | --- | --- |
| `detect` | none | The solution, its projects, the unit, integration and benchmark test projects, the timeout and any overrides error, detected afresh. |
| `package_versions` | `package_id` (required, a NuGet package id); `include_prerelease`: `false` (default, release versions only) or `true`; `take`: 1 to 500, default 20 | Answers at once, no job. Runs `dotnet package search <id> --exact-match --format json [--prerelease]` from the solution directory with no `--source`, so the repository's NuGet.config hierarchy picks the sources, then reads the package source mapping from the files `dotnet nuget config paths` lists (the search itself ignores the mapping). Returns, for each source that lists the package, its newest `take` versions (newest first, NuGet ordering), the version count, the latest stable and prerelease version and `usedByRestore`; plus the overall latest stable and prerelease version from the sources restore would use, and any per-source problems (with user info and query strings cut from the URLs they quote). A package no source lists is a tool error. Use it instead of querying nuget.org, which misses private feeds and ignores the mapping; MSBuild SDK packages work the same way. |
| `restore` | none | Starts `dotnet restore <solution>`. |
| `build` | none | Starts `dotnet restore <solution>`, then `dotnet build <solution> -c Release --no-restore <buildtest properties> -nodeReuse:False` from the solution directory: the whole solution. |
| `test` | `scope`: `all` (default) or `project`; `project`: one of the unit, integration or benchmark test projects | Runs the tests as pre-commit `buildtest` does: restore, build, then test with `--no-restore --no-build --long-running 10 --parallel-algorithm aggressive --ignore-exit-code 8` and the `buildtest` properties. `all` builds the solution, then starts one `dotnet test --solution <solution>` run from the solution directory with `--filter-not-namespace` for `*.Integration.Tests`, `*.Integration.Tests.*`, `*.Benchmark.Tests` and `*.Benchmark.Tests.*`. `project` builds that project, then runs `dotnet test <Project>/<Project>.csproj ... -p:SolutionDir=<src>/` with no namespace filter. A failed restore or build ends the job, so the tests never run against stale binaries. |
| `coverage` | `project`: `all` (default) or one of the unit test projects; `write_report`: `false` (default) or `true`, with `project` `all` only | Starts the coverage run below for each chosen unit test project, one at a time, then combines the results. With `write_report`, a successful run updates the `## .NET` section of `COVERAGE.md` at the repository root and `job_result` gives the file's path in `coverageReportPath`. |
| `buildcheck` | `mode`: `pre_release` (default) or `release` | Starts `dotnet tool restore`, then `dotnet buildcheck -Solution <absolute solution> -WarningAsErrors true -PreReleaseBuild <true\|false>`. |
| `cleanup` | `scope`: `changed` (default) or `all` | Starts `dotnet tool restore`, then `dotnet cscleanup <files>` (in batches of 200). `changed` is the `.cs` files changed against the merge-base with the default branch, committed or not, untracked included; `all` is every tracked `.cs` file. `job_result` lists the files it changed in `changedFiles`. |
| `changelog_lint` | none | Starts `dotnet tool restore`, then `dotnet changelog --changelog <repo>/CHANGELOG.md --lint`. |
| `buildtest` | none | Starts the installed pre-commit `buildtest` script, with no arguments, from the repository root. |
| `job_status` | `id` | Whether the job is still running, how long it has run and which step it is on. |
| `job_result` | `id` | The outcome: state, each step's exit code, test counts, failing test names, coverage, changed files, the masked last 40 lines of output, and the log path. |

Every tool except `detect`, `package_versions`, `job_status` and `job_result` starts a **job** and returns its id at once, so no MCP
call or Bash timeout applies to a long build. Poll `job_status` until `state` is no longer `running`, then read
`job_result`. One job runs at a time; starting another while one is running is refused. A job is killed when it
exceeds its timeout (30 minutes by default, `buildtest` included), and when the server shuts down.

Each step runs as a fixed argument vector, never through a shell. Output is stripped of ANSI codes and masked before
it is logged or returned: the values of environment variables whose names look secret (`*TOKEN*`, `*SECRET*`,
`*PASSWORD*`, `*API_KEY*` and similar), GitHub tokens, `Bearer`/`Basic` credentials, `password=`-style pairs and
credentials in URLs. Lines are capped at 4,096 characters and each log at 16 MiB.

### Quality tools and buildtest

`buildcheck`, `cleanup` and `changelog_lint` run the repository's own .NET tools from its tool manifest, from the
repository root, so the versions are the ones the repository pins. Each job runs `dotnet tool restore` first. A
repository with no `.config/dotnet-tools.json` or `dotnet-tools.json` gets a clear tool error and no job;
`changelog_lint` also needs a `CHANGELOG.md` at the root.

`cleanup` picks its files with git. The default branch is `origin/HEAD`, else the first of `origin/main`,
`origin/master`, `main` and `master` that exists. Files under `bin/` or `obj/`, and files that no longer exist, are
left out. When nothing has changed, no job starts. The files are hashed before and after the run, so `changedFiles`
lists exactly the ones the formatter rewrote.

`buildtest` is the "everything pre-commit checks" run. It runs the installed credfeto-global-pre-commit `buildtest`
script itself rather than a copy of its steps, so the tool follows the script when the script changes. The script
is `BUILD_MCP_BUILDTEST` (an absolute path) if set, otherwise `buildtest` on `PATH`, looked up on every call. If it
cannot be found, the call fails with a message saying so. `build`, `test`, `coverage` and the quality tools stay for
quicker iteration.

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

With `write_report` set to `true`, a run of every unit test project also writes `COVERAGE.md` at the repository root
once it succeeds. Only the `## .NET` section (each project to one decimal place, then `**Overall (.NET)**`) and the
`Captured at commit` line, which names the measured head SHA and the date, are replaced; every other section is kept
byte for byte. A missing file is created with just the `.NET` section and that line. Nothing is written when a test
run fails, a coverage file is missing or unreadable, or the head SHA cannot be read; `coverageErrors` then says why.
`write_report` with a single project is refused, since one project's figures are not the repository's Overall.

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

### Installation

The server ships as a dotnet global tool packaged per platform with Native AOT. The `Credfeto.Build.Dotnet.Server`
package is a small pointer package; the `dotnet` CLI then fetches the matching RID-specific package
(`Credfeto.Build.Dotnet.Server.linux-x64`), which holds only the native executable, so no .NET runtime is needed to
run it:

```bash
dotnet tool install -g Credfeto.Build.Dotnet.Server
```

This puts the command `credfeto-build-dotnet` in `~/.dotnet/tools`. Only `linux-x64` is packaged for now: Native AOT
cannot cross-compile to another architecture, so `linux-arm64` needs to be packed on an arm64 build host.

### Command line

With no arguments the server serves MCP over stdio. It also takes one of these switches:

- `--self-test`: builds the service provider and configuration and resolves every dependency, then exits: `0` when
  that works, `1` with a one-line `self-test failed: <reason>` on stderr when it does not. It starts no MCP transport,
  reads no stdin, opens no network connection and does no repository or file work, so an image build can run
  `credfeto-build-dotnet --self-test` to check the installed tool loads.
- `--help`, `-h`: lists the switches and exits with `0`.

Any other argument, or more than one switch, prints a one-line error on stderr and exits with `2`.

### Packing, publishing and start-up time

For a Native AOT tool the SDK on its own packs only the pointer package from a plain `dotnet pack`, and skips the
RID-specific packages (it can't AOT-compile other RIDs from the outer pack). The server project adds a
`_PackAotRidSpecificToolPackages` target that runs after `Pack` and packs each RID in `ToolPackageRuntimeIdentifiers`
with the same version and output folder, so the plain pack that CI runs produces both
`Credfeto.Build.Dotnet.Server.<version>.nupkg` and `Credfeto.Build.Dotnet.Server.linux-x64.<version>.nupkg`:

```bash
cd src
dotnet pack -c Release
```

Every RID listed in `ToolPackageRuntimeIdentifiers` must be AOT-buildable on the packing host, so the pack has to run
on linux-x64. To pack only the RID-specific package, add `-r linux-x64` and pack the project instead of the solution.
The tool pack takes every file in the publish folder, so delete `bin/Release/net11.0/linux-x64/publish` before
repacking locally.

To get just the binary instead, publish it. The publish fails on any trim or AOT warning:

```bash
cd src
dotnet publish Credfeto.Build.Dotnet.Server/Credfeto.Build.Dotnet.Server.csproj -c Release -r linux-x64
```

From process start to the `tools/list` reply, detection included, the server must take under 200 ms.
`StartupTimeTests` measures this against the native binary: set `BUILD_MCP_SERVER_BINARY` to its path (the published
binary or the installed `credfeto-build-dotnet`) and run the server tests. Without the variable, those tests are
skipped.

### Registering it (oneshot)

The orchestrator's managed MCP config is fixed in the image, so every language server is always registered and each
decides for itself whether it applies. The image installs the tool with `dotnet tool install -g` and registers it in
`claude-managed-mcp.json` as a stdio server:

```json
{
  "mcpServers": {
    "build-dotnet": {
      "type": "stdio",
      "command": "/home/developer/.dotnet/tools/credfeto-build-dotnet",
      "env": { "BUILD_MCP_REPO_ROOT": "/workspace/repo" }
    }
  }
}
```

### Follow-ups

Not in this first version:

- the Node and Python servers on the same core;
- the orchestrator side: installing the tool in the image, registering it in the managed MCP config, allowing its
  tools in managed settings, and then denying raw `dotnet build`, `dotnet test`, `dotnet exec` and
  `dotnet reportgenerator` in Bash;
- a CI step that runs `StartupTimeTests` against the AOT binary;
- a `linux-arm64` tool package, packed on an arm64 host.

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
