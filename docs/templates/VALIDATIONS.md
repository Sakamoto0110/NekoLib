# Validation Evidence Template

**Document ID:** TEMPLATE-VALIDATION-EVIDENCE

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** template for executed module validation evidence

**Surface:** template

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** exclude

Evidence records what ran and its gaps. It does not define behavior or satisfy a
requirement whose boundary or evidence level was not exercised.

```text
## <BOUNDARY>-VALEVID-YYYYMMDD-001

**Requirement IDs:**
**Version:**
**Commit:**
**Tree state:**
**Environment:**
**Targets:**
**Command or scenario:**
**Execution:**
**Evidence level:**
**Result:**
**Artifacts:**
**Gaps:**
**Supersedes:**
```

For soak categories also record duration, workload, concurrency, operations,
faults, expected and actual recovery, resource measurements, acceptance
criteria, crashes, deadlocks, leaked processes/workers/handles, unrecovered
states, resource growth, and cleanup.
