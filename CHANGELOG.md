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

- **NekoLib.Data — breaking pre-stable candidate correction for the first
  `1.0.0` stable family release.** Moved `DatabaseGateway` from
  `NekoLib.Data.Internal.Gateway` to `NekoLib.Data.Gateway` without a shim;
  removed `IUniversalQueryGateway` and the redundant `Get<TTranslator,T>`,
  `Read`, and `StreamData` families; normalized concrete/interface and session
  overloads; exposed parameterized `ContainsData`; removed the unusable
  `IDqlStreamingGateway` and `Microsoft.Bcl.AsyncInterfaces` dependency from
  `net481`; internalized `DbDataReaderExtensions`; sealed concrete types whose
  extension seams are interfaces/composition; and propagated net9 DTO
  reflection metadata through the public interface and mapping paths. See the
  [F1-DATA migration guide](docs/migrations/f1-data.md).
- **NekoLib.Data — additive fluent DELETE surface with a fail-closed behavioral
  guard.** Added `QueryBuilder.DeleteFrom`, `AllowAllRowsDelete`, and matching
  `IDmlGateway`/`DatabaseGateway` builder overloads. Deletes without predicates
  fail by default unless the current statement explicitly opts into all rows;
  builder deletes participate in translation and raise `OnSqlGenerated` before
  dispatch. Raw string overloads remain supported. See the
  [F1-DATA migration guide](docs/migrations/f1-data.md).

### Release governance

- Activated F1 public API and release stability work. Added the coordinated
  SemVer, stability classification, deprecation, compatibility baseline, and
  migration policy. This changes no product assembly or package API.
- Added assembly-derived candidate API snapshots for all 15 library packages
  and both supported targets, plus a deterministic comparison command and the
  cross-target experimental marker rule. This changes no product assembly or
  package API.

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
