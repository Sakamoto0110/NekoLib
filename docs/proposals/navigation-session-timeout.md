# Configurable Session Timeout

**Document ID:** PROPOSAL-NAVIGATION-SESSION-TIMEOUT

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** optional maximum lifetime for an authenticated Navigation session

**Surface:** proposal

**Boundary:** navigation

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## NAV-PROPOSAL-002

**State:** promoted

**Idea:** Let an application configure how long an operator may remain signed in. Measure the interval from sign-in and sign the operator out when it expires, even during continuous use. For example, with an eight-hour limit, an operator who signs in at 08:00 must sign in again at 16:00 even if they interacted with the terminal at 15:59.

**Rationale:** A shared terminal may require fresh authentication once per shift. The existing inactivity timeout renews on observed input, so regular use can keep a session authenticated across a shift change.

**Constraints:** Keep the maximum lifetime disabled by default and configurable independently of inactivity. If both policies are enabled, either deadline can end the session. Navigating to Idle while signed in must preserve the original deadline. A delayed expiration callback from a previous session must leave a newly authenticated session intact.

**Accepted scope:** `PageNavBootstrap.UseSessionTimeout(TimeSpan)` opts in to a positive maximum lifetime. Each successful `NavigationSession.SignIn` starts a fresh deadline. Interaction, page navigation, and direct `GoIdleAsync()` preserve it. Expiration signs out through the existing session-end path and navigates to the resolved Idle page. The Idle page must be configured when this policy is enabled. The inactivity and absolute deadlines remain independent.

**Promotion target:** The owner accepted the bounded implementation on 2026-11-28. Local qualification is complete for the unreleased 2.0.0 family. [Qualification record](../modules/Navigation/VALIDATIONS.md#nav-valevid-20261128-001)

**Source:** [Current Navigation idle behavior](../modules/Navigation/REFERENCE.md#idle-system)
