# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Security
### Added
### Fixed
- .NET server: `test` with scope `all` (the default) now leaves out the `*.Integration.Tests` and `*.Benchmark.Tests` namespaces with `--filter-not-namespace`, as pre-commit `buildtest` does, so benchmark failures CI never sees no longer fail the run. Integration and benchmark test projects can be run by name with scope `project` (#13)
- The default job log folder is now per-user ($XDG_STATE_HOME/credfeto-build-mcp/logs, else ~/.local/state/credfeto-build-mcp/logs, else a credfeto-build-mcp-<uid> folder in the temp directory) and is created owner-only (0700) when BUILD_MCP_LOG_DIR is unset, so users on a shared machine no longer share job logs (#17)
- The solution builds again: long methods in the .NET server DotnetDetector and CleanupFileSelector are split into smaller methods to satisfy the MA0051 method-length analyzer (refactor only, no behaviour change) (#17)
### Changed
- .NET server: `build` and `test` use pre-commit `buildtest`'s settings from one shared list: restore first, then build with `--no-restore`, the `buildtest` `-p:` properties and `-nodeReuse:False`; `test` then runs with `--no-restore --no-build --long-running 10 --parallel-algorithm aggressive --ignore-exit-code 8`. A failed restore or build ends the job, so tests never run against stale binaries (#13)
### Deprecated
### Removed
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->

## [0.0.3] - 2026-11-05
### Fixed
- git commands (and the test helper) no longer inherit the repository-locating `GIT_*` variables git sets for hooks, so the test suite run from a worktree's pre-commit hook no longer commits into, branches or reconfigures the real repository
- Jobs: a job's log file is now flushed and closed as soon as the job finishes (success, failure, timeout or cancellation), before `job_status` reports it finished, so a wait on the log returns straight away instead of running to its timeout
- credfeto-build-dotnet advertises no tools in a repository without a .NET solution, and tests pin the tools/list_changed behaviour for .sln and .slnx (#3)
### Changed
- All projects target net11.0; the redundant Microsoft.Extensions.DependencyInjection.Abstractions package reference is dropped (it is part of the framework)
- SDK - Updated DotNet SDK to 11.0.100-rc.3.26522.148

## [0.0.2] - 2026-11-03
### Fixed
- .NET server: a plain `dotnet pack -c Release` now also packs the `Credfeto.Build.Dotnet.Server.linux-x64` Native AOT tool package next to the pointer package, so CI publishes both and `dotnet tool install -g Credfeto.Build.Dotnet.Server` works

## [0.0.1] - 2026-11-02
### Added
- .NET build, test and coverage MCP server (`credfeto-build-dotnet`), a Native AOT stdio server, and the shared core: job runner, repository detection, Cobertura parsing and `.ai/commands.json` overrides
- .NET server: watches the repository for project and solution changes, re-detects, rebuilds the tool list and sends `notifications/tools/list_changed`; every call is checked against a fresh detection
- .NET server: `buildcheck`, `cleanup` and `changelog_lint` tools that run the repository's own .NET tools, and a `buildtest` tool that runs the installed pre-commit `buildtest` script
- .NET server: packed as a Native AOT dotnet global tool (`dotnet tool install -g Credfeto.Build.Dotnet.Server`, command `credfeto-build-dotnet`) with a `linux-x64` RID-specific package

## [0.0.0] - Project created