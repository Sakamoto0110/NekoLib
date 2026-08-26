# Validation Policy

**Document ID:** GLOBAL-VALIDATION-POLICY

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** validation requirements, evidence taxonomy, reusable profiles, and soak records

**Surface:** policy

**Boundary:** global

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

Validation requirements and validation evidence are different surfaces.
Requirements state what qualification is needed. Evidence states what actually
ran, against which source and environment, with which result and gaps. Evidence
never creates a runtime or API contract.

The existing [test taxonomy](../../tests/README.md) remains authoritative for
test placement and canonical commands. This policy defines the cross-document
vocabulary used by module qualification records.

## Closed taxonomy

| Axis | Values |
|---|---|
| Category | `build`, `focused-regression`, `full-regression`, `api-compatibility`, `package`, `package-consumer`, `runtime`, `interactive-ui`, `automated-ui`, `protocol`, `security`, `performance`, `soak`, `recovery-soak`, `chaos-soak` |
| Execution | `automated`, `manual` |
| Boundary | `in-process`, `filesystem`, `network`, `ipc`, `process`, `database`, `hardware`, `native-ui`, `package-feed`, `deployment` |
| Evidence level | `build-only`, `automated-runtime`, `interactive`, `automated-ui` |

Requirement classification and evidence result are independent:

```text
Requirement: REQUIRED | CONDITIONAL | RECOMMENDED | NOT_APPLICABLE
Evidence:    PASS | FAIL | PARTIAL | NOT_RUN | BLOCKED |
             NOT_APPLICABLE | SUPERSEDED
```

`CONDITIONAL` requirements name their trigger. `NOT_APPLICABLE` records a
reason. A `PASS` names the requirement IDs it satisfies; a run without the
required boundary or level cannot silently satisfy a stronger requirement.

## Profiles

| Profile | Intended boundary |
|---|---|
| `standard-library` | Shipped library baseline |
| `stateful-runtime` | Lifecycle, bounded state, concurrency, cleanup, failure isolation, resources, and soak |
| `external-provider` | Real provider, environment, cancellation, transport loss, recovery, and provider gaps |
| `transport` | Framing or byte stream, timeout, cancellation, disconnect, malformed input, security, and recovery |
| `ui-runtime` | Runtime lifecycle plus native UI behavior and long-running cleanup |
| `platform-adapter` | Platform build, native integration, UI, ownership, and cleanup |
| `supervisor` | Real child processes, crash/recovery, process ownership, protocol, fault injection, and soak |
| `deployment-package` | Published layout, RID selection, package-only consumption, deployment, protocol, and fatal evidence |

Profiles may be combined. A module may add requirements or elevate their
classification. It must not weaken inherited requirements silently; any
exception requires an explicit rationale and review.

Recommended composition routes are structural defaults, not pre-qualified
requirements:

| Boundary | Profiles to evaluate |
|---|---|
| Core, Mvvm, and every shipped library | `standard-library` |
| Logging, Telemetry, Inspection, Navigation | `stateful-runtime` |
| Data, Http | `external-provider` |
| Devices, Pipes | `transport` |
| Navigation | `ui-runtime` |
| Navigation.WinForms, Navigation.Wpf, Diagnostics.Windows | `platform-adapter` |
| Watchdog | `supervisor` |
| Watchdog.Host | `deployment-package` |

The later module campaign derives concrete requirements. The schema can express
real provider and database behavior; TCP, serial, hardware, timeout, late-reply,
disconnect, malformed-input, disposal-race, and soak evidence; native UI and
minidump/WER behavior; and child-process, protocol, crash-loop, cooldown,
orphan-cleanup, deployment, security, recovery-soak, and chaos-soak evidence.
This list describes representational capacity and must not be copied into a
module as requirements without source- and risk-based justification.

Concrete requirements are derived from architecture and risk: lifecycle,
concurrency, resources, external systems, process or protocol boundaries,
failure and recovery modes, security assumptions, incidents, and deployment.
They are not inferred only from the tests that happen to exist.

## Evidence records

Every evidence entry names the version, commit, tree state, environment,
targets, command or scenario, execution mode, evidence level, result, artifacts,
gaps, and superseded evidence. Build, runtime, interactive, package, and
published-package evidence remain separate claims.

Provider, hardware, native UI, security, and deployment evidence is conditional
on its real boundary. A compiled scenario is build-only evidence until it is
launched and observed.

## Soak and recovery soak

A duration alone is not soak qualification. A soak, recovery soak, or chaos soak
records duration, workload, targets, environment, concurrency, operations,
faults, expected and actual recovery, resource measurements, acceptance
criteria, crashes, deadlocks, leaked processes/workers/handles, unrecovered
states, resource growth, artifacts, and cleanup.

The exact closed vocabularies and required record fields are defined in the
[documentation schema](../schemas/documentation-schema.json).
