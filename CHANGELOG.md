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
- Python server `coverage` tool: coverage run -m pytest, per-project rows in COVERAGE.md (#60)
### Fixed
### Changed
- Node test and coverage cap Jest/Vitest workers to the memory limit; memoryPerWorkerMb override (#69)
### Deprecated
### Removed
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->

## [0.0.11] - 2026-11-27
### Added
- Python server (`build-python`): Native AOT tool, pyproject/requirements detection, pip/uv (#58)
- Python server jobs: wheel-only `restore` into `.venv`, `test` (pytest), `lint`, job tools (#59)
- Node `test` tool: optional `paths` and `coverage` parameters (#56)
- build-node: `args` parameter for build and lint tools, and a new `typecheck` tool (#57)
### Fixed
- Node unit `test` in a workspace package runs the root `test:noe2e`, never e2e `test` (#55)

## [0.0.10] - 2026-11-24
### Changed
- MCP tools/list returns tools sorted by name on the .NET and Node servers - #49
- .NET/Node `coverage` always writes `COVERAGE.md` on a full run; report arg removed - #51
- `COVERAGE.md` rows add covered/coverable counts; a single-project run updates its row - #52

## [0.0.9] - 2026-11-19
### Fixed
- `buildcheck`, `cleanup`, `changelog_lint` find the tool manifest in parent directories (#45)
### Removed
- The `changelog_lint` tool; use `changelog_lint` on the changelog MCP server instead (#46)

## [0.0.8] - 2026-11-17
### Added
- Node server (`build-node`): Native AOT tool, npm-only detection, `detect`, repo watch (#39)
- Node server jobs: `restore` (`npm ci`), `build`, `lint`, `test`, test output parsers (#41)
- Node server `coverage` tool reads the Cobertura report and writes the Node section (#43)

## [0.0.7] - 2026-11-16
### Changed
- Core: per-language `.ai/commands.json` sections and `COVERAGE.md` sections (#37)

## [0.0.6] - 2026-11-16
### Added
- `job_result` waits up to `wait_seconds` (0-600, default 240) for a running job (#34)
### Changed
- Language-neutral server code moved into the shared `Credfeto.Build.Server` library (#32)
- Docs say wait with `job_result`, not poll `job_status`; progress every 15 seconds (#34)

## [0.0.5] - 2026-11-15
### Added
- `--self-test` and `--help` command-line switches for credfeto-build-dotnet
- .NET `package_versions` tool lists NuGet package versions from configured sources (#26)
- `coverage` `write_report` argument writes the .NET section of COVERAGE.md (#12)
### Fixed
- Non-absolute `BUILD_MCP_REPO_ROOT` or `BUILD_MCP_LOG_DIR` now fails at startup (#18)
- Default job log folder is per-user and created owner-only (0700) (#17)
- Split long DotnetDetector and CleanupFileSelector methods to satisfy MA0051 (#17)
- Fallback job log folder in temp is random-named and 0700, not a predictable path (#19)
- With no `BUILD_MCP_REPO_ROOT` or `/workspace/repo`, the git top-level folder is used (#29)
### Changed
- SDK - Updated DotNet SDK to 11.0.100

## [0.0.4] - 2026-11-11
### Fixed
- .NET `test` scope `all` skips `*.Integration.Tests` and `*.Benchmark.Tests` namespaces (#13)
### Changed
- .NET `build` and `test` use pre-commit `buildtest` settings; a failed build ends the job (#13)

## [0.0.3] - 2026-11-05
### Fixed
- git commands no longer inherit hook `GIT_*` variables, so tests cannot touch the real repo
- Jobs: the log file is closed as soon as the job finishes, before `job_status` reports it
- credfeto-build-dotnet advertises no tools in a repository without a .NET solution (#3)
### Changed
- All projects target net11.0; redundant DependencyInjection.Abstractions reference dropped
- SDK - Updated DotNet SDK to 11.0.100-rc.3.26522.148

## [0.0.2] - 2026-11-03
### Fixed
- .NET server: `dotnet pack` also packs the `linux-x64` Native AOT tool package

## [0.0.1] - 2026-11-02
### Added
- .NET build, test and coverage MCP server (`credfeto-build-dotnet`) and the shared core
- .NET server: re-detects on project/solution changes and sends `tools/list_changed`
- .NET server: `buildcheck`, `cleanup`, `changelog_lint` and `buildtest` tools
- .NET server: packed as a Native AOT dotnet global tool with a `linux-x64` package

## [0.0.0] - Project created