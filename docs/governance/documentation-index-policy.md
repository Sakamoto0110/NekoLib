# Local Documentation Index Policy

**Document ID:** GLOBAL-DOCUMENTATION-INDEX-POLICY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** rebuildable local Markdown and generated XML API retrieval

**Surface:** policy

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

This policy defines the local SQLite/FTS documentation index used to accelerate
human and agent retrieval after the module-first migration. It defines how the
index is built and interpreted; it does not make the generated database a
repository, product, API, validation, or release authority.

The versioned implementation is
[`eng/documentation_index.py`](../../eng/documentation_index.py). The canonical
refresh command is
[`eng/refresh-documentation-index.ps1`](../../eng/refresh-documentation-index.ps1),
and the read-only consumer is
[`eng/search-docs.ps1`](../../eng/search-docs.ps1). Generated state remains at
`.local/documentation-migration/documentation-index.sqlite3` so the earlier
local migration inventory can be upgraded in place without becoming tracked.

## Indexed corpora

One inventory run records two distinct retrieval layers.

### Authored Markdown and Text

The scanner enumerates Git-tracked and non-ignored untracked `.md` and `.txt`
files. It records Git state, hashes, metadata, headings, line-bounded chunks,
links, and inferred or declared boundary. A missing `Indexing` declaration is
materialized as derived `include` for a present document and `exclude` for a
missing tracked file; absence of an explicit field is not reported as absence
from search.

`Indexing: exclude` suppresses content chunks. `pointer-only` keeps the minimal
portal searchable for routing but does not turn it into a second technical
authority. The document's declared `Authority role` and lifecycle accompany
every result.

### Generated XML API guidance

The refresh first runs the accepted public API verifier. It then discovers the
15 packable managed-library projects and requires exactly one generated XML
document for each of the 30 accepted package/target baselines. The Watchdog Host
deployment package has no managed API layer and remains excluded by design.

Each XML member retains:

- package, assembly, target framework, and module boundary;
- XML member ID and member kind;
- summary, remarks, parameter, return/value, exception, and inheritance text;
- project, generated XML, and accepted-baseline paths; and
- content and artifact hashes tied to the inventory run.

The accepted assembly-derived manifests remain the compiled public API oracle.
Generated XML is searchable API guidance shipped with managed packages. It does
not define runtime behavior, prove semantic accuracy, replace the canonical
module reference, or turn comment presence into documentation correctness.

## Snapshot provenance and freshness

Every run records repository root, branch, full `HEAD`, clean/dirty state, a
fingerprint of the current Git change set and its contents, scanner version,
time, and the hashes of generated XML and accepted baselines.

A snapshot is **fresh** only when:

1. repository root, branch, `HEAD`, and tree fingerprint still match;
2. every indexed XML and baseline file still has its recorded hash;
3. SQLite `integrity_check` is `ok`; and
4. no foreign-key violation exists.

A fresh dirty snapshot may support local investigation, but it is explicitly
reported as `current-dirty` and is not claim-grade. Only a fresh clean snapshot
is reported as `current-clean`. Neither status promotes the database into
evidence: claims still require their normal source, build, test, API, runtime,
package, or release owner.

Search refuses a stale snapshot by default. `-AllowStale` is an explicit
diagnostic override and preserves all stale reasons in the output. It must not
be used to present results as current.

## Canonical commands

Refresh after a clean committed documentation/API campaign:

```powershell
.\eng\refresh-documentation-index.ps1
```

The refresh verifies all accepted compiled API baselines, generates Release XML
assets, appends one complete inventory run, and requires the resulting snapshot
to be current. `-NoBuild` is available only when the caller has already produced
and verified the exact current assemblies, tool output, and XML assets; it does
not waive baseline comparison.

Inspect status without changing the database:

```powershell
.\eng\search-docs.ps1 -Status
```

Search both corpora or one layer:

```powershell
.\eng\search-docs.ps1 -Query "translator extension" -Boundary data
.\eng\search-docs.ps1 -Query "DatabaseGatewayOptions" -Source xml
.\eng\search-docs.ps1 -Query "authority role" -Source markdown -Kind reference
```

`-Json` preserves the same status and provenance for machine consumers. Results
identify path/line/heading for Markdown and package/target/member/XML/baseline
for generated API guidance.

## Campaign integration

A documentation work campaign may declare the refresh as a `post-commit`
finalizer. The clean-tree gate ensures the recorded `HEAD` names all versioned
inputs, and fingerprint deduplication prevents repeating it for the same
campaign change set. Routine edits do not trigger background indexing, a file
watcher, CI, or scheduled work.

The general campaign example treats local indexing as optional because its
absence cannot invalidate versioned documentation. A campaign may make it
required when producing a current index is an explicit completion criterion.
An optional finalizer failure is recorded and reported but does not become PASS
evidence or block otherwise required campaign validation.

## Interpretation and non-goals

The index supports full-text search, metadata filtering, link inspection,
module routing, API-member discovery, and cross-check preparation. It is lexical
SQLite FTS5 retrieval, not embeddings or semantic proof. Search ranking is not
authority ordering, completeness evidence, or a product decision.

The index does not:

- update automatically when documentation changes;
- replace `eng/verify-docs.ps1`, `eng/verify-public-api.ps1`, builds, tests, or
  package consumers;
- infer that an XML comment is correct because it exists;
- update accepted API baselines;
- index ignored private content other than the generated managed XML paths it
  deliberately owns;
- authorize migration, promotion, implementation, commit, push, package,
  publication, or release actions; or
- serve as an opaque mandatory dependency for a clean clone.
