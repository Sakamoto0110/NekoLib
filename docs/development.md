# Development and Validation Guide

**Document ID:** GLOBAL-DEVELOPMENT-GUIDE

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** repository development, compatibility, validation, review, and Git workflow

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

This guide collects the repository-wide practices needed to change NekoLib
safely. It does not replace the sources that own technical truth.

## Authority and baseline

- Source code owns implemented behavior. Project and `Directory.Build.*` files
  own targets, references, compiler settings, build properties, and package
  properties. `NekoLib.sln` owns solution membership.
- Compiled assemblies own the actual public API; accepted manifests under
  `eng/public-api/` own the reviewed API baseline.
- Module `REFERENCE.md` files own current documented contracts.
- `ROADMAP.md` owns product direction, guardrails, and freezes. `TODO.md` is the
  only formally promoted work scheduler. Audits and history are
  baseline-bound evidence, not current implementation authority.
- Before changing the repository, record the branch, full `HEAD`, upstream
  divergence, index state, and working-tree state. Preserve unrelated tracked,
  untracked, and ignored work.

## Build and verification

The [CI workflow](../.github/workflows/ci.yml) runs on pushes and pull requests.
It restores the solution on Windows, builds Release and runs the solution test
suites for .NET Framework 4.8.1 and .NET 9, including Windows desktop targets.
The [NuGet publication workflow](../.github/workflows/publish-nuget.yml) is
manually dispatched with the approved commit and package hashes.

```powershell
dotnet build NekoLib.sln
dotnet test NekoLib.sln
.\eng\verify-docs.ps1
.\eng\verify-premises.ps1
.\eng\verify-public-api.ps1
git diff --check
```

Use the narrowest relevant project, target, or test first. The canonical test
taxonomy and focused commands live in [`tests/README.md`](../tests/README.md).
Launch scenarios under [`runtime_tests/`](../runtime_tests/README.md) as
executables; never treat a compiled scenario as executed runtime evidence.

`src/Tools/BundlerTool/` is outside `NekoLib.sln`. Build it through
`eng/build-bundler.ps1` so generated output and provenance remain under
`artifacts/`. Create local packages only through `eng/pack-local.ps1` with a new
immutable version. `-AllowDirty` is limited to disposable validation builds.

## Compatibility and project configuration

- Preserve both target families unless an accepted public boundary explicitly
  changes them.
- Inspect every affected project file before using a conditional compilation
  symbol or changing nullable, implicit-using, target, reference, or package
  settings. These settings are deliberately not uniform across projects.
- Do not use C# `record` types on a surface shared with `net481` unless the
  project deliberately supplies the required `IsExternalInit` support.
- The current dependency graph is documented in the
  [root module map](../README.md#module-map) and must be confirmed from the
  affected project files. Do not impose Navigation's
  `Contracts`/`Runtime`/platform-adapter layout on unrelated modules.
- Keep Windows-specific behavior in the existing Windows or UI adapter
  projects. Read the affected module reference before changing a module-level
  lifecycle, ownership, concurrency, security, or compatibility contract.

## Tests and runtime evidence

- Unit projects mirror their source modules and test names use
  `MethodName_Condition_ExpectedResult`.
- A test located under `Unit/` may still exercise filesystem, network, IPC, or
  process integration. Report the actual boundary rather than inferring it from
  the directory name.
- Navigation tests that mount the static facade use the
  `NavigationServiceFacade` collection and call
  `await NavigationService.Shutdown()` in a `finally` block.
- The tracked Data fixtures under `tests/NekoLib.Data.Tests/Shared/` are not
  executed database evidence unless a current test or scenario wires them
  explicitly.
- Report source, build, test, API, runtime, interactive, package, publication,
  and external-consumer evidence as separate claims.

## Reviews, documentation, and history

A review or audit is read-only with respect to product code unless its request
also authorizes implementation. Record the exact baseline and scope, verify
claims against current source, projects, tests, and executable evidence, and
separate observations, risks, decisions, and rejected alternatives.

Durable reviews belong under `docs/audit/` or a module `audits/` directory and
remain snapshots. Preserve their original bodies and record later outcomes in
a reconciliation section or current owner. The promotion flow is:

```text
review, finding, issue, proposal, external evidence, or owner decision
    -> accepted direction
    -> TODO.md
    -> implementation
    -> current documentation and evidence
```

Follow the [documentation policy](governance/documentation-policy.md),
[validation policy](governance/validation-policy.md), and affected boundary
manifest. Work campaigns and scoped premises are optional mechanisms governed
by their respective policies; neither creates implementation authority or
replaces validation.

## Git and publication boundary

Do not stage, commit, push, publish, update an accepted API baseline, overwrite
an immutable package version, or create a release unless that action is
explicitly requested. Before an authorized push, recheck the complete diff,
branch, upstream, remote, and working-tree state.
