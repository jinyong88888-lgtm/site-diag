# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [2.0.0] - 2026-11-24

First public release. Bilingual (English / Chinese), single-file, no install.

### Added

- Language selection as the very first screen, bilingual, English by default.
- `--lang en|zh`, `--no-color`, `--version`, `--help` command line options.
- Verdict engine: 12 distinct outcomes, each with an owner and next steps.
- Exit codes: `0` reachable, `1` problem found, `2` usage error or inconclusive.
- Certificate validation, reported separately from handshake success.
- Browser-path check through the system proxy / PAC.

### Changed from the internal 1.x tool

- Output strings moved behind a single translation point, so adding a language is one file.
- DNS servers are now read from the adapter that actually has a default gateway.
  (1.x listed every up adapter, which could show a VPN adapter's resolver and mislead.)
- Repeated TLS attempts (3) to detect intermittent blocking instead of a single sample.

### Fixed

- 1.0 accepted any certificate (validation callback returned `true`), so a replaced
  certificate was reported as a successful handshake. This is now detected and named.
- 1.0 measured only the raw socket path, so a proxy or zero-trust client that blocks the
  browser was invisible and the tool reported "everything is fine". Both paths are compared now.

## [1.1.0] - 2026-11-23

Internal only.

- Added browser-path probe, certificate validation, PAC/VPN detection, intermittent verdict.

## [1.0.0] - 2026-11-23

Internal only. Initial diagnostic build.