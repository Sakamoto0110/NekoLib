# Repository Operational Layout

**Kind:** reference

**Lifecycle:** current

**Subject:** tools, automation, generated artifacts, and machine-local data

These directories have distinct ownership. A file's location determines
whether it can be cited as reproducible repository evidence.

| Path | Owner and lifecycle | Versioned? |
|---|---|---|
| `src/Tools/` | Source code for executables maintained by this repository | yes |
| `tools/` | Locally restored or copied executable payloads; never source authority | no |
| `eng/` | Build, validation, packaging, and repository-maintenance automation | yes |
| `artifacts/` | Generated, disposable build/package/tool output | no |
| `.local/` | Machine-only experiments, configuration, private prerequisites, and scratch data | no |

Tests and shared documentation must not depend on an opaque executable copied
manually into `tools/`. A repository-owned executable needs versioned source and
a reproducible build or restore. An operating-system binary is a declared
prerequisite and must be isolated behind the scenario that uses it; it is not a
vendored repository payload.

## BundlerTool

[`src/Tools/BundlerTool/`](../src/Tools/BundlerTool/) is the only source
authority for BundlerTool. It is deliberately outside `NekoLib.sln` and builds
through:

```powershell
.\eng\build-bundler.ps1
```

The script publishes the net481 executable to
`artifacts/tools/BundlerTool/Release/`, replacing only that disposable output
directory. It emits `build-manifest.json` with the assembly version, source
commit/tree state, project hash, and executable hash. An ignored
`tools/BundlerTool.exe` is at most a local cache and is never proof of the
versioned source's behavior.

The packaging workflow remains separate:
[`eng/pack-local.ps1`](../eng/pack-local.ps1) owns library and Watchdog Host
packages under `artifacts/`.

## Generated catalogs

An LLM-oriented code catalog is not part of Phase C. If separately authorized,
it must reuse or extract BundlerTool's existing Roslyn scanner, produce
deterministic output under `artifacts/`, attach source evidence, distinguish
inference from authored documentation, and never write inferred comments into
product source.
