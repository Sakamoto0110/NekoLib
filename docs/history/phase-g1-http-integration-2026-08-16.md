# Phase G1 Typed HTTP Integration Completion

**Kind:** roadmap/status

**Lifecycle:** historical

**Subject:** completed typed HTTP catalog implementation, deterministic and
package evidence, and external-provider boundary

**Reference date:** 2026-08-16

**Implementation/package reference commit:**
`ae711fb51d27af29701d332a453912ad1f87a029`

**Current state:** [`TODO.md`](../../TODO.md)

This snapshot records completion of the bounded Phase G1 implementation. The
documentation-only archival change that adds this file is intentionally
outside the validated package baseline.

## Delivered architecture

`NekoLib.Http` is an opt-in `net481`/`net9.0` package with no NekoLib project
dependency. An immutable, instance-scoped catalog registers typed GET, POST,
PUT, PATCH and DELETE descriptors. `RelativeUriBuilder` owns relative path and
query escaping, while `HttpApiClient` executes only registered descriptors
through a consumer-owned `HttpClient` and preserves bounded raw HTTP evidence
alongside a typed success value.

The consumer retains ownership of base address, client lifetime, credentials,
certificates, timeout, handlers, retries and other policies. The module owns no
global registry, dependency-injection integration, authentication, secret
storage, logging, provider model or automatic retry behavior.

The optional standalone TheCatAPI scenario uses the public package surface for
typed image search/lookup and run-owned favourite create/query/delete cleanup.
It accepts its key only through `NEKOLIB_THECATAPI_KEY`, uses a random
non-personal `sub_id`, records a sanitized result artifact, bounds every request
and reconciles its own mutations.

## Validation

| Gate | Result |
|---|---|
| Focused HTTP build | Passed on `net481` and `net9.0`; 0 warnings and 0 errors. |
| Deterministic HTTP tests | Passed 16/16 on each target using a controlled `HttpMessageHandler`; no public internet. |
| Scenario build | Passed on `net481` and `net9.0`; 0 warnings and 0 errors. |
| Scenario prerequisite path | Exited `3` on both targets with no provider request, sanitized artifacts, `ApiKeyPresent: false`, and zero cleanup problems. |
| Full solution rebuild | Passed with 0 errors and 515 warning occurrences. Warning verification found no new normalized identity and five baseline identities were not emitted. |
| Full solution tests | Passed 1,281/1,281 serial executions across both target families; 0 failed and 0 skipped. |
| Clean package flow | `1.0.0-local.11` published 16 packages and passed every external package-consumer probe. |
| HTTP package provenance | `NekoLib.Http.1.0.0-local.11.nupkg` contains `lib/net481` and `lib/net9.0`, records commit `ae711fb51d27af29701d332a453912ad1f87a029`, and has SHA-256 `30464eca19e909a993d6e02e84d20b2cf3cb44b909cde3980ffc03cc44b81c1e`. |

The package gate exposed cross-project scheduling sensitivity in the concurrent
solution runner. A confirmed Pipes shutdown race was fixed before packaging;
the Diagnostics timeout test received wider margins; and the package gate now
runs solution projects serially without skipping any test. The final serial
gate and the package flow both passed.

## Evidence boundary and residual work

No real TheCatAPI request was sent because no maintainer-owned key was present.
The missing-key executions prove only prerequisite handling, artifact
finalization and the no-request boundary. They do not prove provider
interoperability, mutation, eventual consistency or cleanup against a live
account.

A real provider run remains optional evidence. If performed, its exit code and
sanitized `result.json` must be inspected together and the provider account must
contain no run-owned favourite afterward. Phase G2 Payments/Pix and Phase F
remain gated; completing G1 does not activate either one.

## Post-closure provider reconciliation — 2026-08-16

After the package and documentation baseline above was closed, a
maintainer-owned key was supplied without being printed or persisted in an
artifact. The real-provider scenario then passed 10/10 with exit `0` on both
`net9.0` and `net481`. Each run proved typed image search/lookup, run-owned
favourite creation/query/deletion, post-delete absence, and final
reconciliation with zero cleanup problems.

The inspected artifacts are
`thecatapi-net9.0-20260816T222349134Z/result.json` and
`thecatapi-net481-20260816T222415829Z/result.json` under the ignored
`artifacts/validation/http/` tree. They contain no credential value, header,
request body, response body, or personal identifier. This later evidence closes
the optional provider gap without changing the immutable package baseline or
activating Phase G2 or Phase F.
