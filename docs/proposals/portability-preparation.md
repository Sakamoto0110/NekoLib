# Portability Preparation Proposal

**Document ID:** PROPOSAL-PORTABILITY-PREPARATION

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** unpromoted non-Windows portability preparation idea

**Surface:** proposal

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## GLOBAL-PROPOSAL-006

**State:** idea

**Idea:** Evaluate intentional portability only after Linux or another platform becomes a real supported target.

**Rationale:** Core contracts already preserve useful platform-neutral seams, but target declarations alone do not prove portable behavior.

**Constraints:** Keep Diagnostics.Windows isolated, audit actual WinAPI use, preserve current Windows behavior, and introduce a platform adapter only for a real target and consumer.

**Promotion target:** An explicit platform-support decision with consumer, package, runtime, and validation requirements.

**Aliases:** F6
