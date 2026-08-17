# Changelog

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible package, public API, compatibility, and migration
changes

This changelog follows the rules in
[`docs/public-api-release-policy.md`](docs/public-api-release-policy.md).
`TODO.md` owns open work; historical implementation and validation narratives
remain under `docs/history/`.

## Unreleased

### Public API

No consumer-visible public API change has been accepted under F1 yet.

### Release governance

- Activated F1 public API and release stability work. Added the coordinated
  SemVer, stability classification, deprecation, compatibility baseline, and
  migration policy. This changes no product assembly or package API.

## Entry format

Future consumer-visible entries must identify:

- the affected package or package family;
- whether the change is additive, behavioral, deprecated, experimental, or
  breaking;
- the intended release version;
- the replacement or migration steps when consumer action is required.

The immutable `1.0.0-local.*` artifacts are pre-stable package candidates, not
individual stable releases. Their build and runtime evidence remains in the
owning completion or scenario records rather than being duplicated here.
