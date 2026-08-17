# Public API and Release Stability Policy

**Kind:** reference

**Lifecycle:** current

**Subject:** public API classification, compatibility, versioning, deprecation,
and release evidence

**Policy decision date:** 2026-08-16

This policy is the accepted foundation for `TODO.md` Phase F1. It defines how
NekoLib turns the current package surfaces into a stable release contract and
how that contract may evolve afterwards. `TODO.md` remains the sole owner of
active work, ordering, and completion state. Source, project files, and compiled
assemblies remain authoritative for the API that actually exists.

## Scope

The coordinated release family contains the 15 library packages produced by
the canonical pack flow. `NekoLib.Watchdog.Host` shares the family version but
is a deployment package: its supported surface is its payload layout, build
targets, bootstrap arguments, and Host/application protocol rather than a
compile-time library API.

Tests, runtime scenarios, BundlerTool, generated artifacts, and the
constants-only `src/Hosting/NekoLib` project are not public package surfaces.
Public types do not become internal merely because their namespace or source
path contains `Internal`.

The repository's `1.0.0-local.*` packages are immutable candidate artifacts.
They provide package and consumer evidence but do not, by themselves, declare
a stable support line. F1 may make intentional candidate-surface corrections
before the first stable family release, subject to an accepted module decision
and migration guidance.

## Stability classes

| Class | Contract |
|---|---|
| **Candidate** | A pre-stable API being evaluated by F1. It may change only through an accepted module item with a recorded API diff and migration. |
| **Stable** | A supported public or protected contract. Compatibility follows the SemVer and deprecation rules below. After the first stable baseline, an unmarked public API is stable by default. |
| **Experimental** | An explicitly identified opt-in API whose design may still change. Its marker and documentation must work across every target that exposes it; a namespace, naming convention, or audit note is not enough. Changes still require changelog entries and migration guidance. |
| **Implementation detail** | Code that is not externally visible in the compiled package. `internal`, private, and package-excluded code may evolve without public API compatibility promises, while runtime behavior must still preserve supported contracts. |

The F1 baseline covers public and protected types and members, including
extension and implementation contracts intended for consumer customization.
`InternalsVisibleTo` access for tests does not promote an internal member into
the supported public API.

## Coordinated versioning

NekoLib uses one coordinated family version. Every package produced by one
release run is built, tested, and published together. Consumers should align
NekoLib package versions; compatibility of arbitrary mixed family versions is
not promised unless a release record explicitly says otherwise.

After the first stable release:

- a **patch** may fix defects and documentation without removing or changing a
  stable source or binary contract;
- a **minor** may add compatible stable APIs, introduce deprecations, and evolve
  explicitly experimental APIs;
- a **major** may contain approved breaking changes with migration guidance;
- a bug fix that changes observable behavior must identify the documented
  contract it restores. A behavior change is not automatically compatible just
  because the method signature is unchanged.

NekoLib currently supports one active stable family line at a time. Immutable
older packages may remain available, but no parallel maintenance or backport
window is promised. Supporting multiple active lines requires a separate
roadmap decision that names their support and security windows.

## Compatibility boundary

A compatibility review must consider source, binary, behavioral, and package
compatibility. Breaking changes include, but are not limited to:

- removing, renaming, moving, or reducing the accessibility of a public or
  protected type or member;
- changing signatures, generic constraints, base types, implemented
  interfaces, virtuality, abstract requirements, nullability contracts, or
  contract-significant attributes;
- changing an inlined public constant or a default value on which consumers
  compile or rely;
- changing documented ownership, disposal, ordering, threading, cancellation,
  exception, serialization, protocol, or lifecycle behavior;
- changing package IDs, target assets, dependency requirements, build targets,
  or deployment layout in a way that invalidates a supported consumer.

The absence of repository references does not prove that a public member is
unused. A removal decision must describe the intended consumer boundary,
impact, rejected alternatives, and migration path.

## Deprecation and breaking-change approval

Once a stable baseline exists, a stable API is normally marked with
`[Obsolete(message, error: false)]` for at least one released minor version and
removed only in the next major version. The message must name the replacement
or explain that there is none. Changing an obsolete warning to
`error: true` is removal-equivalent and follows the same approval rule.

Before the first stable baseline, a candidate API may be corrected without a
deprecation release when the accepted F1 module decision records the break and
provides migration guidance. Compatibility shims are not automatic: retain one
only when an actual consumer or a declared support window justifies its cost.

An exceptional stable break requires an explicit roadmap decision, a concrete
reason such as security or an impossible-to-preserve contract, affected-package
and target details, migration guidance, and a coordinated major release unless
the decision documents why that is impossible.

## Automated API baseline

F1.1 will establish an assembly-derived API manifest and compatibility check
for every library package and target framework. Source searches and syntax
catalogs may help discovery, but compiled package assets are the compatibility
oracle.

The check must:

- compare the accepted surface separately for each package and target;
- capture public and protected signatures plus compatibility-significant
  metadata such as nullability, attributes, constants, inheritance, interface
  implementation, and virtuality;
- distinguish an intentional target-specific API from an accidental target
  mismatch;
- emit a reviewable diff and fail when an unapproved baseline change occurs;
- update a baseline only in the same narrow change that carries the accepted
  decision, implementation, tests, changelog, and migration record.

The existing `EnablePackageValidation` setting remains part of packaging. It
validates package structure and cross-target compatibility, but without a
historical baseline it does not replace the F1 compatibility check against an
accepted prior surface.

## Module finalization workflow

Each module is completed as a small, independently reviewable block:

1. Establish the exact commit and working-tree scope, then inventory the
   compiled public surface on both target families.
2. Identify supported consumer entry points, extension contracts, ownership,
   lifecycle, and package boundaries. Do not decide from public-type counts.
3. Classify the candidate surface and record proposed keeps, additions,
   removals, moves, or replacements in a read-only review.
4. Promote only accepted decisions to `TODO.md`. A facade is introduced only
   when it expresses a real ownership or lifecycle boundary; Navigation's
   static facade is not a repository-wide template.
5. Implement the accepted delta narrowly, preserving project references,
   target-specific contracts, and unrelated supported modes.
6. Validate both targets, focused tests, relevant external PackageReference
   consumers, API diff, documentation links, and a new immutable candidate
   package version when package bits change.
7. Update the current module documentation, `CHANGELOG.md`, migration guidance,
   and the accepted API baseline before marking the module complete.

Navigation's stability-sensitive core remains frozen during inventory and
review. F1 does not authorize runtime changes to a frozen component; any such
change still needs a confirmed finding and a separate module-scoped unfreeze.

## Release records

[`CHANGELOG.md`](../CHANGELOG.md) records consumer-visible changes. Each public
API entry identifies the affected package, compatibility class, release target,
and migration link or inline migration. Historical audits remain evidence
snapshots and are not rewritten as release notes.

A stable family release requires:

- every shipped module to have an accepted API classification and baseline;
- no unexplained API or cross-target diff;
- current changelog and migration guidance;
- a clean, immutable package-family build from an identified commit;
- passing package validation and external PackageReference consumer probes;
- the validation evidence required by the existing repository release flow.

F1 defines local, manual release stability. CI evaluation remains gated under
F2 and is not activated by this policy.
