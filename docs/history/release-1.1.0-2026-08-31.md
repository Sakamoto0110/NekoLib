# NekoLib 1.1.0 Release Completion

**Kind:** roadmap/status

**Lifecycle:** historical

**Subject:** completed qualification and publication of the first compatible minor

**Reference date:** 2026-08-31

**Reference commit:** `49bd3ec243983ce7a19d222e0f698226e734cf09`

**Current state:** [`CHANGELOG.md`](../../CHANGELOG.md), the
[`public API and release policy`](../public-api-release-policy.md), and the
[`1.1.0` stable release record](../stable-release-1.1.0.md)

## Outcome

`RELEASE-1.1.0` qualified, materialized, tagged, published, and independently
verified the coordinated `1.1.0` family. The SemVer-minor basis is the additive
stable Data surface for structured QueryBuilder operations and explicit
write/read type adaptation. The warning-only legacy QueryBuilder overloads
remain available and `NEKOMKT-F026` remains scheduled only for an explicitly
opened `2.0.0` removal window.

This record replaces the completed release entry that formerly occupied live
`TODO.md`. It does not duplicate the full package hash tables or become current
API authority.

## Qualification and materialization

- Clean package source: `1147f76beb412c3ae6368088bc0c22eb4653daa8`.
- Candidate: `1.1.0-local.9`; stable version: `1.1.0`.
- Release rebuild: 202 warning occurrences, 0 errors, and no new normalized
  warning identity.
- Solution tests: 1,787 passed, 0 failed, 0 skipped.
- Public API: all 30 accepted manifests passed without updates.
- Package sets: 16 main and 15 symbol artifacts; managed XML documentation was
  present for both target families in every managed package.
- External consumers and Watchdog Host package/runtime probes passed for the
  candidate, exact stable set, and fresh public downloads.

## Publication and external verification

- Annotated tag `v1.1.0` resolves to the clean package-source commit.
- GitHub Release ID `380040305` preserves the 31 approved assets with aggregate
  SHA-256
  `C3E0764831636A74CD4969BDC609B16EAEF86FBE0A375C6741217F0F386E7F43`.
- Trusted-publication run
  [`33437988158`](https://github.com/Sakamoto0110/NekoLib/actions/runs/33437988158)
  verified that aggregate before OIDC login and published every main and symbol
  package.
- Run `33437837425` failed safely on draft-release access before hash, OIDC, or
  push. The required temporary permission was removed after the successful run.
- All 16 NuGet.org IDs expose `1.1.0`. Fresh main-package downloads passed
  repository-signature verification and differed from the approved bytes only
  by `.signature.p7s`.
- The full public-package consumer flow passed on `net481` and
  `net9.0-windows`, including the expected unsupported `win-arm64` failure and
  both packaged Host protocol/startup/stop probes.

## Evidence boundary

This release added no new interactive UI, physical hardware, external provider,
performance, soak, recovery, or production-fleet run. Earlier evidence for
those layers remains scoped to its recorded source. The stable release record
owns individual hashes, commands, publication details, signed-download hashes,
and the separation between source, build, test, API, package, runtime, release,
and unexecuted evidence.
