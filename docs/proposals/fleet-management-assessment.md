# Fleet Management Assessment Proposal

**Document ID:** PROPOSAL-FLEET-MANAGEMENT-ASSESSMENT

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** unpromoted fleet-management product assessment

**Surface:** proposal

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## GLOBAL-PROPOSAL-005

**State:** idea

**Idea:** Assess whether managing many installed terminals warrants a separate companion product or agent for enrollment, identity, configuration, updates, rollback, credential rotation, evidence collection, offline behavior, API, and operator UI.

**Rationale:** Fleet operation may become valuable at scale, but it is not part of the current local application-framework contract.

**Constraints:** Keep NekoLib local; keep Watchdog a local supervisor; do not force fleet concerns into Core, Navigation, Diagnostics, or Watchdog; a companion may consume NekoLib packages without joining the base framework.

**Promotion target:** A product-level use case, ownership decision, threat model, and separate-product boundary assessment.

**Aliases:** F5
