# Navigation Surface Regions Proposal

**Document ID:** PROPOSAL-NAVIGATION-SURFACE-REGIONS

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** unpromoted Navigation surface-region and toast-orchestration idea

**Surface:** proposal

**Boundary:** navigation

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## NAV-PROPOSAL-001

**State:** exploring

**Idea:** Explore opt-in owner-scoped surface regions, beginning with bounded stacked or queued toast notification and independently dismissible entries.

**Rationale:** A region could provide deterministic visual ownership and lifetime without turning transient surfaces into pages or navigation contexts.

**Constraints:** No participation in `Current`, history, guards, page reuse, or page lifecycle; keep current single/replacement toast behavior as default; preserve UI-thread ownership, bounded queues, teardown, hit testing, DPI, and native z-order in adapters; no nested `NavigationContext`.

**Promotion target:** A bounded Navigation design review that finalizes ownership, handles, capacity/overflow, timers, movement, adapter behavior, and any narrow frozen-core impact.

**Aliases:** F7

Any required change to `NavigationContext`, `NavigationRuntime`, `PageRegistry`,
or `PageFactory` also requires a separate explicit unfreeze.
