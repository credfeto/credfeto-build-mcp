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
| `Credfeto.Build.Server` | The language-neutral MCP server infrastructure every server shares (AOT-compatible library, not packed on its own): the command line and self-test, the stdio host, the live tool list that follows the repository, the repository watcher, the `job_status`/`job_result` tools, the tool argument and schema helpers, the tool errors and the short command runner. |
| `Credfeto.Build.Dotnet.Server` | The `.NET` server (`credfeto-build-dotnet`), a Native AOT executable. |
| `Credfeto.Build.Node.Server` | The Node server (`credfeto-build-node`), a Native AOT executable. |
| `Credfeto.Build.Testing` | Test support shared by the test projects: temporary directories, isolated git, a recording job runner and a minimal MCP client. |
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

The repository the server works on is **never** a tool argument. It is `BUILD_MCP_REPO_ROOT` when that is set. When it
is unset, it is `/workspace/repo` if that folder exists (the agent container's mount), and otherwise the git top-level
folder of the directory the server was started in (`git rev-parse --show-toplevel`; git is only run in that case). If the
variable is unset, `/workspace/repo` is missing and the start directory is not in a git checkout (or git can't be run),
the server stops at startup with exit code 1 and a message on stderr saying so. When serving, the server writes one line to stderr naming the
chosen root and where it came from, for example
`credfeto-build-dotnet: repository root /src/app (the git root of the current directory; BUILD_MCP_REPO_ROOT is not set and the default folder does not exist)`;
stdout carries only MCP traffic. A value that is set but is not an absolute path (relative, empty or whitespace)
stops the server at startup (and fails `--self-test`) with exit code 1 and a message on stderr naming the variable
and the value; it is never swapped for the default. `BUILD_MCP_LOG_DIR` follows the same rule. Every command runs with a fixed working directory under that root,
so the agent never composes a `cd`.

| Variable | Default | Meaning |
| --- | --- | --- |
| `BUILD_MCP_REPO_ROOT` | `/workspace/repo` when it exists, else the git root of the start directory | The repository to work on. Must be an absolute path when set; anything else is a startup error. Unset with neither fallback available is a startup error too. |
| `BUILD_MCP_LOG_DIR` | `$XDG_STATE_HOME/credfeto-build-mcp/logs`, else `~/.local/state/credfeto-build-mcp/logs`, else, when there is no home directory, a new private folder (`credfeto-build-mcp-<random>`, mode 0700) that the server creates in `$TMPDIR` at startup | Where each job writes its full log (`<job id>.log`, mode 0600) in a per-user folder with mode 0700. Must be an absolute path when set; anything else is a startup error. A relative `XDG_STATE_HOME` or `HOME` is ignored when choosing the default. The temporary-directory fallback gets a new random name each start, so another user can't create it first or put a symlink in its place; if it can't be created, startup fails with a message to set `BUILD_MCP_LOG_DIR`. |
| `BUILD_MCP_BUILDTEST` | `buildtest` on `PATH` | The absolute path of the pre-commit `buildtest` script that the `buildtest` tool runs. When set to anything else (including empty) the `buildtest` tool fails with a message naming the variable. |

### Tools

Every parameter is an enum, a pattern-checked id (a job id, or a NuGet package id), a flag or a bounded whole
number. No tool takes a free-form argument string, and a value outside those bounds is refused without anything
being run.

The `buildtest` properties that `build` and `test` pass are `-p:ApiCompatGenerateSuppressionFile=true`,
`-p:ContinuousIntegrationBuild=true`, `-p:IsProduction=false`, `-p:Optimize=true`,
`-p:SuppressNETCoreSdkPreviewMessage=true` and `-p:Version=0.0.0.1-test`.

| Tool | Parameters | What it does |
| --- | --- | --- |
| `build` | none | Starts `dotnet restore <solution>`, then `dotnet build <solution> -c Release --no-restore <buildtest properties> -nodeReuse:False` from the solution directory: the whole solution. |
| `buildcheck` | `mode`: `pre_release` (default) or `release` | Starts `dotnet tool restore`, then `dotnet buildcheck -Solution <absolute solution> -WarningAsErrors true -PreReleaseBuild <true\|false>`. |
| `buildtest` | none | Starts the installed pre-commit `buildtest` script, with no arguments, from the repository root. |
| `cleanup` | `scope`: `changed` (default) or `all` | Starts `dotnet tool restore`, then `dotnet cscleanup <files>` (in batches of 200). `changed` is the `.cs` files changed against the merge-base with the default branch, committed or not, untracked included; `all` is every tracked `.cs` file. `job_result` lists the files it changed in `changedFiles`. |
| `coverage` | `project`: `all` (default) or one of the unit test projects | Starts the coverage run below for each chosen unit test project, one at a time, then combines the results. A successful run of `all` always rewrites the `## .NET` section of `COVERAGE.md` at the repository root; a single-project run replaces only its assembly's row and sums Overall again (see Coverage below). `job_result` gives the file's path in `coverageReportPath`. |
| `detect` | none | The solution, its projects, the unit, integration and benchmark test projects, the timeout and any overrides error, detected afresh. |
| `job_result` | `id`; `wait_seconds`: 0 to 600, default 240 | Waits up to `wait_seconds` for the job to finish, then returns the outcome: state, each step's exit code, test counts, failing test names, coverage, changed files, the masked last 40 lines of output, and the log path. `0` answers at once. |
| `job_status` | `id` | A single, instant look at whether the job is still running, how long it has run and which step it is on. |
| `package_versions` | `package_id` (required, a NuGet package id); `include_prerelease`: `false` (default, release versions only) or `true`; `take`: 1 to 500, default 20 | Answers at once, no job. Runs `dotnet package search <id> --exact-match --format json [--prerelease]` from the solution directory with no `--source`, so the repository's NuGet.config hierarchy picks the sources, then reads the package source mapping from the files `dotnet nuget config paths` lists (the search itself ignores the mapping). Returns, for each source that lists the package, its newest `take` versions (newest first, NuGet ordering), the version count, the latest stable and prerelease version and `usedByRestore`; plus the overall latest stable and prerelease version from the sources restore would use, and any per-source problems (with user info and query strings cut from the URLs they quote). A package no source lists is a tool error. Use it instead of querying nuget.org, which misses private feeds and ignores the mapping; MSBuild SDK packages work the same way. |
| `restore` | none | Starts `dotnet restore <solution>`. |
| `test` | `scope`: `all` (default) or `project`; `project`: one of the unit, integration or benchmark test projects | Runs the tests as pre-commit `buildtest` does: restore, build, then test with `--no-restore --no-build --long-running 10 --parallel-algorithm aggressive --ignore-exit-code 8` and the `buildtest` properties. `all` builds the solution, then starts one `dotnet test --solution <solution>` run from the solution directory with `--filter-not-namespace` for `*.Integration.Tests`, `*.Integration.Tests.*`, `*.Benchmark.Tests` and `*.Benchmark.Tests.*`. `project` builds that project, then runs `dotnet test <Project>/<Project>.csproj ... -p:SolutionDir=<src>/` with no namespace filter. A failed restore or build ends the job, so the tests never run against stale binaries. |

Every tool except `detect`, `package_versions`, `job_status` and `job_result` starts a **job** and returns its id at once, so no MCP
call or Bash timeout applies to a long build. Call `job_result`, which waits for the job to finish; if `state` is
still `running` when the wait runs out, the figures are partial, so call it again. While it waits it sends
`notifications/progress` (the steps completed out of the step count, and the step running) to a client that asked for
them with a progress token, at least every 15 seconds, so the client's request timeout keeps being reset. Use
`job_status` for a single look at progress, never in a loop. Cancelling a `job_result` call only stops the wait; the
job keeps running. One job runs at a time; starting another while one is running is refused. A job is killed when it
exceeds its timeout (30 minutes by default, `buildtest` included), and when the server shuts down.

Each step runs as a fixed argument vector, never through a shell. Output is stripped of ANSI codes and masked before
it is logged or returned: the values of environment variables whose names look secret (`*TOKEN*`, `*SECRET*`,
`*PASSWORD*`, `*API_KEY*` and similar), GitHub tokens, `Bearer`/`Basic` credentials, `password=`-style pairs and
credentials in URLs. Lines are capped at 4,096 characters and each log at 16 MiB.

### Quality tools and buildtest

`buildcheck` and `cleanup` run the repository's own .NET tools from its tool manifest, from the repository root, so
the versions are the ones the repository pins. Each job runs `dotnet tool restore` first. The manifest is found the
way `dotnet tool run` finds it: `.config/dotnet-tools.json`, then `dotnet-tools.json`, in the repository root and then
in each parent directory up to the filesystem root, so a manifest shared by several checkouts works. With no manifest
anywhere up the tree the tool gives a clear error and starts no job.

`buildcheck` stays a shell-out because the `FunFair.BuildCheck.Runner` library cannot yet be compiled into this
Native AOT server: its `global.json` loader uses reflection-based JSON deserialization (IL2026/IL3050).

`CHANGELOG.md` is linted by `changelog_lint` on the separate changelog MCP server, not by this server.

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

A run of every unit test project (`project` `all`) always writes `COVERAGE.md` at the repository root once it
succeeds; there is no option to skip it. Only the `## .NET` section and the
`Captured at commit` line, which names the measured head SHA and the date, are replaced; every other section is kept
byte for byte. A missing `.NET` section is inserted before the other language sections (the order is .NET, Node,
Python, Shell). A missing file is created with just the `.NET` section and that line. The update holds an exclusive
lock on the file from the read to the write, so another build server writing its own section at the same moment
waits rather than losing either section. Nothing is written when a test
run fails, a coverage file is missing or unreadable, or the head SHA cannot be read; `coverageErrors` then says why.

Each row records the covered and coverable line counts after the percentage (one decimal place), and the Overall row
is the sum of the rows' counts. The percentage stays in the second column, where the coverage ratchet reads it:

```text
## .NET

| Project | Line Coverage | Covered | Coverable |
| --- | --- | --- | --- |
| Credfeto.Foo | 82.1% | 1234 | 1503 |
| Credfeto.Bar | 0.0% | 0 | 0 |
| **Overall (.NET)** | **82.1%** | **1234** | **1503** |
```

A component with no coverable lines is `0.0%` with counts of 0, and an `n/a` row with no counts counts as 0 of 0.

A single-project run updates `COVERAGE.md` too, under the same lock: it replaces only the row of the project's
assembly (adding it in order if it is new) and sums Overall again from every row's counts, so the section matches what
a run of `all` gives for the same code (each test project measures only its own assembly). `coverageReportNote` in
`job_result` says what it did. When another row has no counts (a file written before they were recorded), Overall is
left as it was and `coverageReportNote` names those rows; the next run of `all` fills them in. A single-project run
never creates the file or a missing `.NET` table, and writes nothing when its run did not measure its assembly;
`coverageErrors` says why.

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
the repository. `timeoutMinutes` is clamped to 1-240.

Each language's section is read on its own, so the file can also carry a `node` section (`enabled`, `packages`,
`testScript`, `coverageScript`, `coverageFile`, `timeoutMinutes`) for the Node server, and a `python` section, without
the .NET server rejecting it. An unknown property inside the `dotnet` section, or a `dotnet` value of the wrong type,
makes the server ignore only that section. An unknown top-level setting is reported but leaves the known sections in
force. Invalid JSON or a file over 64 KiB make the server ignore the whole file. `detect` reports why in every case,
without echoing the file's content.

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
  reads no stdin, opens no network connection and does no file work, so an image build can run
  `credfeto-build-dotnet --self-test` to check the installed tool loads. The only repository work is locating the root
  as serving does: with `BUILD_MCP_REPO_ROOT` unset and no `/workspace/repo` it asks git for the start directory's top
  level, and fails outside a git checkout. It prints no repository root line when it passes.
- `--help`, `-h`: lists the switches and exits with `0`.

Any other argument, or more than one switch, prints a one-line error on stderr and exits with `2`.

### Packing, publishing and start-up time

For a Native AOT tool the SDK on its own packs only the pointer package from a plain `dotnet pack`, and skips the
RID-specific packages (it can't AOT-compile other RIDs from the outer pack). `src/NativeAotTool.targets`, which the
server project imports, adds a `_PackAotRidSpecificToolPackages` target that runs after `Pack` and packs each RID in
`ToolPackageRuntimeIdentifiers` with the same version and output folder, so the plain pack that CI runs produces both
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

- the Node server's restore, build, lint, test and coverage jobs, and the Python server;
- the orchestrator side: installing the tool in the image, registering it in the managed MCP config, allowing its
  tools in managed settings, and then denying raw `dotnet build`, `dotnet test`, `dotnet exec` and
  `dotnet reportgenerator` in Bash;
- a CI step that runs `StartupTimeTests` against the AOT binary;
- a `linux-arm64` tool package, packed on an arm64 host.

## The Node server

`Credfeto.Build.Node.Server` (command `credfeto-build-node`, registered as the `build-node` MCP server) is the second
language server. It is built on `Credfeto.Build.Server`, like the .NET server. It detects npm repositories and
restores, builds, lints, tests and measures coverage for them, and adds or updates their dependencies, as jobs.

### Node detection

- **Install roots** are a `package.json` at the repository root and one in `src/`, the two places the CI npm action
  looks.
- **Workspaces:** a root's npm `workspaces` field, either an array or `{ "packages": [...] }`, adds its packages. Only
  literal directories and single-level `dir/*` globs are supported. Deeper globs, negations, paths that leave the root
  and anything under `node_modules` are skipped.
- **npm only.** Each install root needs a `package-lock.json` or an `npm-shrinkwrap.json`. Installs always go through
  npm, so an npm lock file is used even when another package manager's lock file sits next to it.
  - A root with only a `bun.lock`, `bun.lockb`, `pnpm-lock.yaml` or `yarn.lock`, or one whose `packageManager` names
    another manager, is reported as unsupported.
  - A root with no lock file is reported as such, because `npm ci` needs one.
- **Packages:** each package's name (its directory when `package.json` has none), its install root, which of the
  `build`, `lint`, `test`, `test:noe2e` and `test:coverage` scripts it has, and the dependencies it declares in
  `dependencies`, `devDependencies` and `optionalDependencies` are recorded.
- **Test runner:** Vitest, Jest or `node --test`, from the first word of the package's test script (after any
  `VAR=value` assignments), otherwise from a `vitest` or `jest` dependency. It is used only to read the test output;
  an unknown runner reports exit codes alone.
- **Limits:** each `package.json` is read up to 1 MiB, and a file that is larger, or that is not a JSON object, is
  reported.
- **Overrides:** the `node` section of `.ai/commands.json` applies `enabled`, `packages` (narrows the detected
  packages by name, and reports any name that matches nothing), `testScript` (the script `test` runs instead of its
  own choice, for the packages that already have it; an override selects, it never adds) and `timeoutMinutes`
  (default 30, at most 240).

When nothing supported is found, the server **registers no tools**, and its instructions give the reason.

### Following a Node repository

Each listed directory is watched **without recursion**: the repository root, `src/`, `.ai/`, and the workspace
directories and glob parents found at start-up. A recursive watch on Linux adds one kernel watch per directory, so a
repository with `node_modules` would run out of watches. Changes to `package.json`, the lock files or
`.ai/commands.json`, and directories coming and going, are debounced and detected again. When the tools change, the
server sends `notifications/tools/list_changed`.

Only workspace directories that existed at start-up are watched. One added later is picked up by the next change in a
watched directory, or by the next `detect` call, which always detects afresh.

### Node tools

| Tool | Parameters | What it does |
| --- | --- | --- |
| `add_package` | `package_id`, `version` (optional), `dev`, `package` | `npm install <id>@<version>` into one package's `package.json` (`--save-dev` with `dev`), then lists the version installed. |
| `build` | `package` | `npm run build`; offered only when some package has a `build` script. |
| `coverage` | `package` | Runs the package's own coverage script and reads the Cobertura report it writes; offered only when some package has a `test:coverage` script, or the script `node.coverageScript` names. With `package` `all`, a successful run always writes the `## Node` section of `COVERAGE.md`; a single-package run replaces only its own row. See Node coverage below. |
| `detect` | none | Lists the install roots and their lock files, the packages with their directories, install roots, scripts and test runners, the timeout and any overrides error, as they are now. |
| `job_result`, `job_status` | `id`; `wait_seconds` on `job_result` | As for the .NET server: `job_result` waits for the job to finish. |
| `lint` | `package` | `npm run lint`; offered only when some package has a `lint` script. |
| `restore` | none | `npm ci --ignore-scripts --no-audit --no-fund` in every install root. The only install path. |
| `test` | `scope` (`unit` default, `all`), `package` | `unit` runs `test:noe2e` where a package has it (as the pre-commit hook does), otherwise `test`; `all` runs `test`. `node.testScript` replaces the choice. Returns pass, fail and skip counts and failing test names for Vitest, Jest and `node --test`. |
| `update_package` | `package_id`, `version` (optional, `latest` when left out), `package` (`all` default) | `npm install <id>@<version>` in each chosen package whose `package.json` declares it, keeping its section, then lists the version installed. |

Every tool except `detect`, `job_status` and `job_result` starts a job and returns its id at once.

- **`package`:** `all`, or one of the packages that has the script. A workspace package runs from its install root
  with `--workspace=<directory>`. For `all`, an install root whose own package has the script runs it once at the
  root, as the pre-commit hook does, otherwise each workspace package with the script runs.
- **Installing first:** `build`, `lint` and `test` run `npm ci --ignore-scripts` first when
  `node_modules/.package-lock.json` is missing or older than the lock file, so a test run does not reinstall every
  time. A failed install stops the job.
- **Environment:** every step gets `CI=true` (so a bare `vitest` does not go into watch mode), `NO_COLOR=1`,
  `FORCE_COLOR=0`, and npm's update notifier, fund and audit messages turned off.
- **npm:** it is resolved to an absolute path from `PATH` and started with an argument vector, never through a shell,
  and `npx` is never used, so nothing is downloaded and run on the fly.
- **Package tools:** `package_id` must be an npm registry name and `version` a version, range or dist-tag, both
  matched against fixed patterns, so no URL, path, git or alias specification reaches npm. Every install passes
  `--ignore-scripts`.

### Node coverage

The `coverage` tool only runs a script the repository already has: `test:coverage`, or the script
`node.coverageScript` names, in each chosen package that has it. The server never adds a coverage command or reporter
flags of its own, so what is measured is exactly what the repository configured; a package with neither script gets
no coverage tool.

- **Report:** the script must write a Cobertura report, as Vitest's `cobertura` reporter does. The server reads
  `coverage/cobertura-coverage.xml` in the package's directory, or `node.coverageFile` (a relative path inside the
  package; an absolute path or one with a `..`, `.` or empty segment is refused with an overrides error and the
  default used). A report from an earlier run is deleted first.
- **Figures:** each package is one row, named after the package, with all of its report's directories merged; the
  shared Cobertura parser and aggregator give per-package and Overall line coverage. The report's `DOCTYPE` is
  skipped, never fetched or expanded.
- **`COVERAGE.md`:** a successful run of every package (`package` `all`) always replaces the `## Node` section and the commit line of
  `COVERAGE.md` at the repository root, with one decimal place and the same table as the `.NET` section, and leaves
  every other section as it is. It is not written when a run fails, a report is missing or unreadable, or the head
  SHA cannot be read; `coverageErrors` says why. There is no option to skip the report. Rows carry covered and
  coverable line counts, as in the `.NET` section.
- **One package:** a single-package run replaces only that package's row of the `## Node` table and sums
  Overall (Node) again from every row's counts, exactly as a single-project .NET run does (including leaving Overall
  unchanged, and saying why in `coverageReportNote`, when a row has no counts).

### Installing and running the Node server

It installs, packs and starts like the .NET server:

- install it with `dotnet tool install -g Credfeto.Build.Node.Server`;
- check it with `credfeto-build-node --self-test`;
- a plain `dotnet pack` in `src/` produces both `Credfeto.Build.Node.Server` and
  `Credfeto.Build.Node.Server.linux-x64`.

`StartupTimeTests` in `Credfeto.Build.Node.Server.Tests` time the published binary named by `BUILD_MCP_SERVER_BINARY`.

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
