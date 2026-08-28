# Public API Documentation and Extensibility Review — 2026-08-27

**Kind:** audit

**Lifecycle:** historical

**Subject:** managed public API documentation coverage and consumer extension
contracts

**Reference date:** 2026-08-27

**Reference commit:** `78d8ce0061b9e8cfab87ab88db5c8ed1832eb4bd`

**Last reconciliation:** 2026-08-28

**Current state:** extension guidance and member coverage completed for all five
`NEKOMKT-F009` family subtasks; the integrated package gate remains closed

**Reviewed tree:** public source, project files, accepted API manifests, tests,
and current technical references at `HEAD`, plus the documentation-only changes
recorded below. The tree already contained unrelated documentation/governance
changes in `ROADMAP.md`, `TODO.md`, `docs/README.md`, `docs/audit/README.md`, and
the NekoMarketplace evidence-intake audit; they were preserved and are not
findings of this review.

## Outcome

The public API is **not fully documented**. All 30 accepted compiled API
baselines match their Release assemblies, but an opt-in XML-documentation rebuild
initially found 1,122 unique `CS1591` diagnostics in public source. Targeted
documentation of confirmed extension seams reduced that count to 1,080. Fourteen
of the fifteen managed public assemblies still have missing XML comments;
`NekoLib.Diagnostics.Windows` is the only zero-diagnostic assembly in this scan.

Every managed package family has a current technical reference, but those
references intentionally explain architecture, lifecycle, ownership, and
behavior rather than catalog every symbol. The accepted manifests remain the
exact compiled-surface authority; they are not API documentation.

The extension review confirmed supported consumer implementation seams in Core,
Data, Devices, Diagnostics, HTTP, Inspection, Logging, Navigation, Pipes, and
Telemetry. It also corrected an important classification error: Data capability
interfaces such as `IDqlGateway` and `IDmlGateway` are consumption views, not
plug-ins used by `DatabaseGateway`.

## Evidence model

Three different layers answer different questions:

| Layer | Question answered | Authority |
|---|---|---|
| Release assembly plus `eng/public-api/<package>/<tfm>.txt` | What public metadata is actually compiled and accepted? | exact public surface |
| XML comments in public source | What appears in IDE/tooling symbol help? | member-level documentation |
| Current module technical reference | How does the contract behave and how should a consumer compose or extend it? | normative behavioral guidance |

No layer substitutes for another. A passing API-manifest check does not prove
documentation coverage, and a symbol name appearing in a README does not prove
that its implementation contract is explained.

## Method

1. Enumerated the 15 packageable managed library families and both accepted TFM
   manifests for each family.
2. Ran `eng\verify-public-api.ps1 -NoBuild` against the current Release
   assemblies.
3. Rebuilt the solution with `GenerateDocumentationFile=true` and collected
   source `CS1591` diagnostics.
4. Removed the project/TFM suffix and deduplicated the same source diagnostic
   emitted by multiple targets or build passes.
5. Inspected current module references, extension types, their call sites, and
   focused tests before documenting extension behavior.

`CS1591` is a presence check only. It does not grade accuracy, examples,
parameter documentation, inherited documentation, or prose quality. The counts
therefore must not be converted into a documentation percentage without a
separately defined denominator and quality policy.

## XML documentation coverage

The initial scan reported 1,122 unique source diagnostics. After documenting the
selected extension contracts, the same rebuild reported:

| Managed assembly | Residual unique `CS1591` |
|---|---:|
| `NekoLib.Core` | 98 |
| `NekoLib.Data` | 318 |
| `NekoLib.Devices` | 4 |
| `NekoLib.Diagnostics` | 34 |
| `NekoLib.Diagnostics.Windows` | 0 |
| `NekoLib.Http` | 53 |
| `NekoLib.Inspection` | 27 |
| `NekoLib.Logging` | 17 |
| `NekoLib.Mvvm` | 8 |
| `NekoLib.Navigation` | 200 |
| `NekoLib.Navigation.WinForms` | 89 |
| `NekoLib.Navigation.Wpf` | 79 |
| `NekoLib.Pipes` | 94 |
| `NekoLib.Telemetry` | 3 |
| `NekoLib.Watchdog` | 56 |
| **Total** | **1,080** |

The largest residual concentrations are Data, Navigation, Core, Pipes, and the
two Navigation adapters. This is a repository-wide documentation backlog, not a
reason to infer that those APIs are unreviewed: the accepted compiled manifests
still match. Conversely, the matching manifests do not make the missing XML
documentation complete.

`NEKOMKT-F009` in `TODO.md` is a separate packaging decision: it promotes
shipping existing XML documentation assets and explicitly does not wait for full
public-member coverage. This review does not broaden that promoted scope or open
its implementation gate.

## Consumer extensibility inventory

| Area | Confirmed implementation seam | Current guidance after this review |
|---|---|---|
| Core / Logging | `ILogSink`, `IFlushableLogSink` | custom sink lifecycle, ordering, flush, and failure behavior already documented in the Logging reference |
| Core / Telemetry | `ITelemetrySink` | inline dispatch, ordering, failure isolation, reentrancy, and backpressure already documented in the Telemetry reference |
| Core / Inspection | `IInspectionRecorder` and read-only snapshot/capability sources | custom recorder and passive evidence boundaries already documented in the Inspection reference |
| Data | `IDbConnectionFactory`, `IDbQueryTranslator`, `TypeValueConverter` through promotion/decay/materialization rules | implementation recipes and invariants added to the Data reference; targeted XML comments added |
| Devices | `ICommTransport`, `IHardwareProtocol`, `IProtocolWithLogging`, and `StreamCommTransport` | transport/protocol ownership and the abstract stream transport are already described in the Devices reference |
| Diagnostics | `CrashDumpWriter` | custom writer path, return, timeout, `None`, and lifecycle rules added to the Diagnostics reference |
| HTTP | `IHttpBodySerializer` | media type, request/response type, failure, ownership, and bypass rules added to the HTTP reference; targeted XML comments added |
| Mvvm | `ViewModelBase` virtual notification funnel | subclass contract is maintained in the Mvvm reference |
| Navigation | `IGuard`, `GuardAttribute`, `IPlatformAdapter` and its specialized platform/view contracts | custom guard-attribute and named-event adapter limits added to the Navigation reference; targeted XML comments added |
| Pipes | `IPipeMetrics` | concurrency, synchronous callback, isolation, snapshot, and non-authority rules added to the Pipes reference; targeted XML comments added |
| Diagnostics.Windows / Watchdog | no additional general-purpose public interface or abstract plug-in seam was confirmed | compose the shipped concrete/application APIs; do not infer extension from public constructors or delegates with other purposes |

The Devices row names its own seams explicitly to avoid suggesting that the Data
factory contract crosses module boundaries. Each module retains its actual
architecture; this review introduces no repository-wide plug-in abstraction.

## Data translator contract

`IDbQueryTranslator.Translate(QueryModel)` is the supported provider SQL-shaping
seam for `QueryBuilder` operations. A custom translator:

- is synchronous and must not open connections, dispatch commands, or own retry
  policy;
- receives provider-neutral SQL, `Top`, structured logical parameters, and the
  command policy;
- should return through `DatabaseQuery.FromLogicalParameters(...)` so logical
  identity, provenance, type-adaptation rules, values, and command policy survive;
- must preserve logical parameter names and placeholder semantics;
- must preserve occurrence order when positional binding can apply, especially
  for OleDb;
- is bypassed by raw string operations, whose event lifecycle starts at
  `OnSqlDispatch` rather than `OnSqlGenerated`; and
- does not by itself make an unknown provider eligible for automatic
  schema-validated promotion or representation fallback.

The focused Data tests demonstrate parameter pass-through, dialect-specific row
limits, null rejection, repeated positional placeholders, and the unknown-
provider adaptation boundary. The current source exposes no public constructor
for `LogicalParameter`; renaming or inventing generated parameters is therefore
not the normal extension contract.

`IDatabaseGateway`, `IDqlGateway`, `IDmlGateway`, `IDtoQueryGateway`, and their
sibling interfaces let a consumer depend on narrower capabilities. The concrete
gateway does not call back into consumer implementations of those interfaces, so
they must not be advertised as translator-style extension points.

## Additional extension constraints made explicit

- A custom `CrashDumpWriter` is invoked as a bounded contributor, receives the
  reserved path and configured level, and must return `true` only after producing
  the file. A configured custom writer still receives `CrashDumpLevel.None`.
- `IHttpBodySerializer.Serialize` receives the runtime request-body type;
  `Deserialize` receives the endpoint response type and must return an assignable,
  non-null value. String/no-content/non-success responses bypass deserialization.
- `IPipeMetrics` callbacks may be concurrent, synchronous, and failure-isolated.
  `Snapshot()` is consumer-called and is not failure-isolated by the transport.
- A consumer-defined `GuardAttribute` can create an `IGuard`, but the built-in
  `RedirectTo` wrapper is not externally reusable. Custom redirect semantics must
  be implemented by the returned guard.
- `IEventSubscriptionAdapter` is an optional named-event service registered by
  bootstrap. Current Navigation lifecycle code does not consume it, and consumers
  resolving it own their matching detach calls.

## Changes applied by this review

- Added targeted XML documentation to Data translators/models/factories and the
  type-converter delegate, HTTP serializer contracts, Pipes metrics contracts,
  and Navigation guard/event-subscription contracts.
- Expanded the Data reference with custom connection-factory, translator, and
  type-rule guidance, including ownership, parameter, OleDb, event, and provider-
  recognition boundaries.
- Expanded the HTTP, Pipes, Diagnostics, and Navigation references for their
  confirmed consumer extension seams.
- Added this dated audit and registered it in the documentation indexes.

No runtime behavior, project reference, target framework, package configuration,
or accepted API manifest was changed. No residual finding was promoted to
`TODO.md` or `ROADMAP.md`.

## Residual findings

### DOC-001 — full XML member coverage is incomplete

The post-change scan retains 1,080 unique `CS1591` diagnostics. Completing this
requires a separately accepted quality policy: scope, inheritance treatment,
generated/compatibility members, required tags, warning enforcement, and rollout
order. This audit records the evidence but does not schedule that work.

### DOC-002 — XML completeness and package delivery are independent

The repository can ship XML files while many members remain undocumented, and
can document members in source without shipping those files in packages. Both
need separate validation. `NEKOMKT-F009` owns only the delivery decision already
recorded in `TODO.md`.

### NAV-EXT-001 — custom guard redirect ergonomics are asymmetric

`GuardAttribute.RedirectTo` is public, but the helper used by built-in attributes
to apply it is internal. The current behavior is now documented rather than
silently implying parity. Changing that API or behavior would require a separate
Navigation public-API decision and explicit unfreeze; it is not authorized by
this documentation review.

## Reconciliation — 2026-08-27

After this review recorded `DOC-001` and `DOC-002`, the owner expanded the
accepted `NEKOMKT-F009` scope. Full member coverage and package delivery now form
one promoted item with five independently executable family subtasks: Navigation,
Data, Core, Pipes, and the remaining managed-module tail. This changes current
scheduling only; it does not rewrite the review's original evidence, counts, or
the distinction between coverage evidence and package-delivery evidence.

## Validation

- `eng\verify-public-api.ps1 -NoBuild`: all 30 accepted baselines matched after
  the documentation edits.
- Release rebuild with `GenerateDocumentationFile=true`: passed, 0 errors;
  1,080 residual unique source `CS1591` diagnostics after targeted comments.
- `dotnet test tests/NekoLib.Data.Tests/Unit/NekoLib.Data.Tests.Unit.csproj
  --no-restore --configuration Release`: passed, 177 `net481` plus 186 `net9.0`
  tests.
- `eng\verify-docs.ps1`: passed, including the untracked audit candidate; the
  verifier correctly notes that an untracked file is not clean-clone evidence
  until committed.
- `git diff --check`: passed; line-ending conversion notices were informational.

No package build, PackageReference consumer probe, external provider scenario,
or interactive Navigation runtime scenario is claimed by this review; none is
needed to establish documentation coverage or the source-confirmed extension
contracts.

## Reconciliation — `NEKOMKT-F009-TAIL` implementation — 2026-08-27

The owner opened only the Tail family gate. The completed boundary comprises
`NekoLib.Watchdog`, `NekoLib.Http`, `NekoLib.Diagnostics`,
`NekoLib.Diagnostics.Windows`, `NekoLib.Inspection`, `NekoLib.Logging`,
`NekoLib.Mvvm`, `NekoLib.Devices`, and `NekoLib.Telemetry`.
`NekoLib.Watchdog.Host` remains outside this managed-library documentation
family as a deployment/tools package.

The refreshed planning inventory reproduced the Tail baseline exactly: 202
unique `CS1591` diagnostics across both target families, deduplicated by public
member. Every public member now has effective XML documentation, and existing
comments were read against current source, tests, accepted manifests, and the
normative module references rather than accepted from presence alone. The final
Tail rebuild reports zero `CS1591` and zero malformed/unresolved XML-comment
warnings. Eight projects build with no warnings; Devices retains 11
pre-existing nullable warning identities, emitted once per target, and adds no
new identity.

The consumer-extension review remains module-specific. Existing Logging,
Telemetry, Inspection, HTTP, Diagnostics, Mvvm, and Watchdog guidance was
retained and aligned with the completed member contracts. The Devices reference
now adds a minimal `IHardwareProtocol` implementation and explains when to
derive from `StreamCommTransport`, the ownership inherited from that base, the
requirements of a direct `ICommTransport` implementation, and why adjacent
logging/data contracts are not plug-in discovery surfaces. No new extension
model, runtime behavior, project reference, namespace, target framework, or
public API was introduced.

Tail implementation evidence:

- documentation-enabled Release rebuilds passed for all 18 Tail target
  assemblies; generated XML files were present beside all 18 assemblies;
- all 18 accepted public API manifests matched without baseline updates;
- the eight focused module suites passed 666 tests across both target families;
  the Watchdog modern suite initially had one process-marker failure while all
  suites ran concurrently, then the failed test and the complete 106-test
  modern suite passed when rerun sequentially; its 106-test `net481` suite had
  already passed;
- `eng\verify-docs.ps1 -BuildLogPath <final Core rebuild log>` passed, found
  no new warning identity, and validated the untracked audit candidate; the
  file is not clean-clone evidence until committed; and
- `git diff --check` passed; line-ending conversion notices were informational.

This closes only the Tail portion of `DOC-001`. Navigation, Data, Core, and
Pipes retain their separate closed implementation gates. `DOC-002` also remains
open at the integrated level: no package was built, and neither package content
nor PackageReference XML-documentation delivery is claimed. The final
`NEKOMKT-F009` package gate remains closed until all five family subtasks finish.

## Reconciliation — `NEKOMKT-F009-PIPES` implementation — 2026-08-27

The owner next opened only the Pipes family gate. The current project, package,
solution entry, source-adjacent normative reference, accepted manifests, and
both target assemblies confirmed one package boundary: `NekoLib.Pipes`, with no
project dependency, targeting `net481` and `net9.0`. Pipes has not yet migrated
to the module-first pilot and therefore has no boundary `MANIFEST.md`; this work
preserved the source-adjacent reference selected by the current documentation
index instead of performing an incidental structural migration.

The refreshed documentation-enabled rebuild reproduced the planning baseline:
94 unique source `CS1591` diagnostics across the two target compilations. These
map to 93 public member names because the target-specific `PipeMessage.Data`
property occupies separate `NET481` and `NET9` declarations. The completed
rebuild reports zero `CS1591`, zero malformed or unresolved XML-comment
warnings, zero other warnings, and zero errors. Both generated XML files are
present and contain no blank member entry unless that entry uses an intentional
`<inheritdoc />` from the fully documented `IPipeMetrics` contract.

Existing comments were reviewed against current lifecycle, framing, metrics,
error, security, and target behavior. In particular,
`IPipeMetrics.OnServerResponseSent` now correctly describes elapsed handler plus
response-write-attempt time rather than handler time alone. Public comments now
cover configuration capture and validation, resource ownership, one-shot and
terminal lifecycle, callback ordering and self-wait constraints, concurrency,
cancellation and timeout boundaries, response-versus-exception behavior,
target-specific payload DOMs, sensitive payload and metrics evidence, and
snapshot semantics. Existing implicit parameterless constructors were made
explicit only so those accepted manifest members can carry XML documentation;
the compiled public API remained unchanged.

The normative Pipes reference now includes a complete minimal custom
`IPipeMetrics` implementation, callback and snapshot invariants, redaction
ownership, and the explicit boundary that it is the package's only
consumer-implemented public interface. Handler registration, event callbacks,
snapshot DTO constructors, and standalone endpoint composition are not plug-in
discovery or activation, and framing, serialization, stream creation, retry,
and authorization remain non-replaceable package/application boundaries.

Pipes implementation evidence:

- the documentation-enabled Release rebuild generated `NekoLib.Pipes.xml`
  beside both target assemblies and passed with 0 warnings and 0 errors;
- both accepted public API manifests matched without baseline updates;
- the focused real named-pipe suite passed 74 `net481` and 74
  `net9.0-windows` tests with `-m:1`;
- `eng\verify-docs.ps1` passed and validated the untracked audit candidate;
  the file is not clean-clone evidence until committed; and
- `git diff --check` passed; line-ending conversion notices were informational.

This closes only the Pipes portion of `DOC-001`. The Navigation, Data, and Core
family gates remain closed, retaining 784 planning-baseline diagnostics between
them. `DOC-002` remains open at the integrated level: no package was built, and
neither package content nor PackageReference XML-documentation delivery is
claimed. The final `NEKOMKT-F009` package gate remains closed until those three
families also finish.

## Reconciliation — `NEKOMKT-F009-CORE` implementation — 2026-08-28

The owner next opened only the Core family gate. Current project, package,
solution, source, accepted manifests, tests, and documentation authorities
confirmed one zero-dependency boundary: `NekoLib.Core`, targeting `net481` and
`net9.0` with one identical public surface. Core has not migrated to the
module-first pilot and therefore has no boundary `MANIFEST.md`; this work
preserved the source-adjacent normative reference selected by the current
documentation index instead of performing an incidental structural migration.

The refreshed documentation-enabled rebuild reproduced the planning baseline
exactly: 98 unique public `CS1591` diagnostics across the two target
compilations. Every existing public comment was judged against the current
source, accepted manifests, focused tests, concrete Logging, Telemetry, and
Inspection references, and the historical F1 disposition rather than accepted
from presence alone. The completed rebuild reports zero `CS1591`, zero
malformed or unresolved XML-comment warnings, zero other warnings, and zero
errors. Both generated XML files contain the same 109 documented member entries,
with no blank entry and no entry missing both a summary and intentional
inheritance.

Member contracts now describe ownership, synchronous call boundaries, optional
capabilities, completion and registration lifecycles, snapshot ordering and
budgets, null-object behavior, outer-copy versus shallow-value semantics,
caller-supplied time and evidence assertions, failure behavior, and sensitive
data responsibilities. Existing summaries were expanded where they were too
vague; no source comment was treated as proof of completeness merely because it
already existed.

The normative Core reference now classifies all eleven public interfaces. The
usual extension paths are custom Logging and Telemetry sinks, while complete
`ILogger`/`ITelemetry` implementations and their optional flush/snapshot
capabilities, plus alternate Inspection recorder/snapshot providers, remain
supported explicit-composition contracts. It records their invariants and a
minimal custom telemetry-sink example. Models, enums, extension conveniences,
null singletons, `Disposable.Empty`, callback registrations, and the explicit
`InspectionProvider` slot are not plug-in discovery or activation. The
experimental `IInspectionRecorder.RegisterAction` contract remains
`NEKOEXP0001`; documentation did not stabilize, expand, or unfreeze it.

Core implementation evidence:

- the documentation-enabled Release rebuild generated `NekoLib.Core.xml`
  beside both target assemblies and passed with 0 warnings and 0 errors;
- both accepted public API manifests matched without baseline updates;
- the focused Core suite passed 13 `net481` and 13 `net9.0` tests;
- a source-diff comparison after removing XML-comment and blank lines confirmed
  zero product-code change;
- `eng\verify-docs.ps1` passed and validated the untracked audit candidate;
  the file is not clean-clone evidence until committed; and
- `git diff --check` passed; line-ending conversion notices were informational.

This closes only the Core portion of `DOC-001`. The Navigation and Data family
gates remain closed, retaining 686 planning-baseline diagnostics between them.
`DOC-002` remains open at the integrated level: no package was built, and
neither package content nor PackageReference XML-documentation delivery is
claimed. The final `NEKOMKT-F009` package gate remains closed until those two
families also finish.

## Reconciliation — `NEKOMKT-F009-DATA` implementation — 2026-08-28

The owner next opened only the Data family gate. Current source, project and
solution topology, accepted manifests, tests, migrations, historical reviews,
and the source-adjacent normative reference confirmed one independent package:
`NekoLib.Data`, targeting `net481` and `net9.0` with no NekoLib project
reference. Data has not migrated to the module-first pilot and therefore has no
boundary `MANIFEST.md`; this work preserved its current README authority instead
of performing an incidental structural migration.

The refreshed documentation-enabled rebuild reproduced the planning baseline:
318 unique public source locations lacked XML documentation. Those locations
represented 317 distinct member names because one target-conditional public
surface occupied two declarations. The scan also found one existing unresolved
`DbDataReader` cref. Every existing public comment was judged against current
implementation, target-specific manifests, focused tests, migration guidance,
and accepted Data reviews rather than accepted from presence alone. The
completed rebuild reports zero `CS1591`, zero malformed or unresolved
XML-comment warnings, zero other warnings, and zero errors.

The generated `net481` XML contains 471 documented member entries, including 45
intentional `<inheritdoc />` entries. The `net9.0` XML contains 484 entries,
including 48 intentional inherited entries; its additional member surface is
the target-specific asynchronous streaming family, while DTO signatures also
carry target-specific annotations. No generated entry is blank unless it
intentionally inherits a documented base or interface contract.

Member contracts now describe query construction and trusted SQL boundaries,
parameter metadata and OleDb positional ordering, connection and session
ownership, nested transaction semantics, synchronous observer ordering and
failure isolation, buffered/callback/stream cleanup, raw textual fidelity loss,
dynamic IL process-wide limits, DTO mapping, type-adaptation policy and
value-free evidence, cancellation, and target differences. The unresolved cref
was corrected, and existing comments were expanded where their presence did not
provide a useful contract.

The normative Data reference documents the three supported consumer
implementation seams: `IDbConnectionFactory`, `IDbQueryTranslator`, and
`TypeValueConverter` as embedded in promotion, decay, and materialization rules.
It includes composition guidance, implementation examples, provider and
ownership invariants, logical-parameter preservation, explicit loss
authorization, and DTO-property-scoped read adaptation. Gateway interfaces such
as `IDatabaseGateway`, `IDqlGateway`, and `IDmlGateway` remain consumer capability
views, not provider plug-in seams. Options and events configure and observe;
sealed gateway, context, builder, row, and generic-factory types are not
inheritance contracts.

Data implementation evidence:

- the documentation-enabled Release rebuild generated `NekoLib.Data.xml` beside
  both target assemblies and passed with 0 warnings and 0 errors;
- both accepted public API manifests matched without baseline updates;
- the focused Data suite passed 177 `net481` and 186 `net9.0` tests with `-m:1`;
- a source-diff comparison across 27 modified C# files after removing XML-comment
  and blank lines confirmed zero product-code change;
- `eng\verify-docs.ps1` and `eng\verify-skills.ps1` passed; the former validated
  this untracked audit candidate, which is not clean-clone evidence until
  committed; and
- `git diff --check` passed; line-ending conversion notices were informational.

This closes only the Data portion of `DOC-001`. The Navigation family gate
remains closed with its 368-diagnostic planning baseline. `DOC-002` remains open
at the integrated level: no package was built, and neither package content nor
PackageReference XML-documentation delivery is claimed. The final
`NEKOMKT-F009` package gate remains closed until Navigation also finishes.

## Reconciliation — `NEKOMKT-F009-NAV` implementation — 2026-08-28

The owner opened only the Navigation family gate. Current source, the three
project files, solution membership, six accepted manifests, focused tests,
historical F1 reviews, and the source-adjacent normative reference confirmed
three managed packages: `NekoLib.Navigation` (`net481`; `net9.0`) and its
WinForms/WPF adapters (`net481`; `net9.0-windows`). Navigation has not migrated
to the module-first pilot and therefore has no boundary `MANIFEST.md`; this work
preserved the current source-adjacent README authority and did not perform an
incidental structural migration.

The refreshed documentation-enabled rebuild reproduced the promoted planning
baseline exactly: 200 residual `CS1591` diagnostics in Navigation core, 89 in
WinForms, and 79 in WPF, for 368 total. Every existing public comment was judged
against current source, compiled surfaces, tests, and the normative reference.
That review also found three pre-existing ambiguous `Control.BeginInvoke` cref
references that a missing-comment count could not expose; they now identify the
delegate overload explicitly. The completed rebuild reports zero `CS1591`, zero
malformed, unresolved, or ambiguous XML-comment warnings, and zero errors in all
three projects and both target families.

Each target now generates the matching XML file beside its assembly. The core
XML has 436 member entries with 23 intentional `<inheritdoc />` entries;
WinForms has 121 entries with 65 inherited entries; WPF has 111 entries with 55
inherited entries. Both targets of each package have identical counts, and no
entry is blank unless it intentionally inherits the documented base or
interface contract.

Member contracts now cover metadata precedence, page construction, session and
history ownership, guard evaluation and redirects, facade events and teardown,
diagnostic correlation, platform dispatch, modal blocking, focus and idle
observation, native host/view ownership, toolkit coordinates, and adapter
cleanup. Frozen `NavigationContext`, `PageRegistry`, and `PageFactory` received
documentation only; `NavigationRuntime` behavior and the canonical lifecycle
order were not changed.

The normative Navigation reference now consolidates the supported extension
boundary: page and surface views, custom guards, `PageFactory` registration,
full UI-platform ports, optional host toolkit capability, and Core-owned
Logging/Telemetry/Inspection writers. It records composition routes and
invariants plus a minimal custom page-factory example. It also states that
`IUserContext`, runtime service capabilities, DTOs/events, and optional event
subscription infrastructure do not become plug-in discovery or replaceable
framework services merely because they are public.

Navigation implementation evidence:

- documentation-enabled Release rebuilds generated all six target XML assets
  with zero XML-documentation diagnostics and zero errors;
- all six accepted public API manifests matched without baseline updates;
- the focused Navigation suite passed 292 `net481` and 292
  `net9.0-windows` tests with `-m:1`;
- `eng\verify-docs.ps1` passed against each project rebuild log, and
  `eng\verify-skills.ps1` passed;
- `git diff --check` passed; line-ending conversion notices were informational;
  and
- no package, PackageReference consumer, interactive UI scenario, runtime soak,
  commit, or push was performed or claimed.

This closes `DOC-001` across all five managed families. `DOC-002` remains open:
the XML assets exist in build output, but package contents and PackageReference
delivery have not been validated. All family prerequisites are now satisfied;
the final immutable-package gate remains separate and closed pending explicit
authorization.
