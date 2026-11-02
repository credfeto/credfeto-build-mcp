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
### Changed
### Deprecated
### Removed
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->

## [0.0.1] - 2026-11-02
### Added
- .NET build, test and coverage MCP server (`credfeto-build-dotnet`), a Native AOT stdio server, and the shared core: job runner, repository detection, Cobertura parsing and `.ai/commands.json` overrides
- .NET server: watches the repository for project and solution changes, re-detects, rebuilds the tool list and sends `notifications/tools/list_changed`; every call is checked against a fresh detection
- .NET server: `buildcheck`, `cleanup` and `changelog_lint` tools that run the repository's own .NET tools, and a `buildtest` tool that runs the installed pre-commit `buildtest` script
- .NET server: packed as a Native AOT dotnet global tool (`dotnet tool install -g Credfeto.Build.Dotnet.Server`, command `credfeto-build-dotnet`) with a `linux-x64` RID-specific package

## [0.0.0] - Project created