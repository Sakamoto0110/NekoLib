# Navigation WinForms/WPF Adapter Review — 2026-08-03

**Kind:** audit

**Lifecycle:** historical

**Subject:** code-first review of the WinForms and WPF Navigation platform
adapters — UI-thread dispatch, host and view lifecycle, overlay surfaces, focus
and light dismissal, idle integration, DPI, and bootstrap wiring

**Reference date:** 2026-08-03

**Reference commit:** `ae1781086b3858cdc9cb025473ed18e3445ee1eb`

**Last reconciliation:** 2026-08-04 — NAV-001's native repeat closed the last
open finding; see the residual-gaps section

**Current state:** every accepted finding is implemented; the authoritative
active-work list remains [`TODO.md`](../../TODO.md) Phase E2

**Reviewed baseline:** the `NekoLib.Navigation.WinForms` and
`NekoLib.Navigation.Wpf` product files, their shared `NekoLib.Navigation`
contracts, and `tests/NekoLib.Navigation.Tests/Unit`, on a clean worktree on
branch `navigation-claude`. Both adapters built on `net481` and
`net9.0-windows`, the 222 Navigation unit tests passed on both target families,
and the WPF smoke scenario built.

**Authority:** this file preserves the evidence, the accepted directions, the
rejected alternatives, and the reconciliation. `TODO.md` is the sole
authoritative active-work list. The review turn itself changed no product code;
implementation followed as separate commits, one per finding.

---

## Review outcome

The two adapters were structurally sound — the layering held, no finding
required a change to `NavigationContext`, `NavigationRuntime`, `PageRegistry`,
or `PageFactory`, and none authorized one. What the review found instead was a
cluster of defects that **headless tests could not see and had not seen**: only
eight of the 222 tests touched a native adapter at all, and none of them covered
dispatch, focus, the interaction observer, the timer, or any view base.

Three findings share a single root cause worth stating plainly: **a platform
helper was trusted to answer a question it cannot answer before the host is
realized.**

- `Control.InvokeRequired` answers `false` on *every* thread while no window
  handle exists in the parent chain, so the WinForms dispatcher ran navigation
  work on the calling thread and the exception it documents was unreachable in
  exactly the case it described (NAV-006).
- `UIElement.Focusable` is `false` on `UserControl`, which every shipped WPF view
  base derives from, so the WPF `Focus` guard made focus an unconditional no-op
  for every surface (NAV-003).
- `Control.LostFocus` does not bubble and is not subtree-scoped, and
  `IViewHost.Focus` forwards focus to a child, so the container whose
  `LostFocus` the WinForms focus observer subscribed to never held focus to lose
  (NAV-004).

The second theme is **contract text that did not match behavior**: "tap anywhere
to dismiss" was accurate on neither platform and differed between them, popover
light dismissal follows focus and not hit testing on both platforms and said so
nowhere, and the error a consumer sees on the most common misuse named a
`NavigationService.Initialize` member that does not exist.

The third is **wiring that was designed and then never connected**:
`INavigationSurface` already returned the nine anchor points and a DPI factor and
documented itself as being "used to position overlays, dialogs, keyboards, debug
panels", but `PageNavBootstrap` never constructed or registered an
`INavigationToolkit`, so no view could obtain one — and the WinForms toast base,
alone among the surface bases, never undid the host's `Dock = Fill` and covered
the whole navigation host (NAV-010). Likewise `PageFactory.Warn` had no
subscriber anywhere, so every page and every surface was silently created through
the migration-only default-constructor fallback (NAV-008d).

## Promotion reconciliation — 2026-08-03

Findings are ordered by impact, as promoted. Every one was confirmed against
current source before promotion; none was promoted on a historical document.

| Finding | Accepted disposition | Implemented at |
|---|---|---|
| NAV-001 idle transition cancelled by sign-out UI mutation | Treat the transition as admitted once the pre-sign-out check passes; revalidate disposal, `StopIdle()` and context ownership after `SignOut()`, but stop comparing the interaction generation | `dc4f1c7` |
| NAV-002 authentication denial had no diagnostic reason | Return `Authentication required.`, matching the role and permission guards | `36bb778` |
| NAV-003 WPF surfaces never received keyboard focus | Focus the first focusable descendant, retry once the surface is `Loaded`, keep a view that focuses its own control from `OnShownAsync` winning | `1d775c1`, closed `4ab9629` |
| NAV-004 WinForms focus-loss dismissal never fired | Observe `Control.Leave` (subtree-scoped) instead of `LostFocus`; keep `Form.Deactivate` | `af7e97b`, closed `1de5e50` |
| NAV-005 WPF blocker destroyed `IsEnabled` bindings | Write through `SetCurrentValue`; restore only elements actually disabled | `16df26d`, closed `4ab9629` |
| NAV-006 WinForms dispatch untruthful without a handle | Capture the owning thread at construction; inline only on the real UI thread, throw anywhere else | `f4805a5` |
| NAV-007 surface dismissal reachability undocumented and wrong | Keep the click bindings, document the real per-platform reachability, treat an explicit close affordance as the supported dismissal for a toast with child controls | `c96321d`, `22200a2`, closed `70ef4eb` |
| NAV-008 seven small confirmed defects | Correct all seven; give `Warn` a consumer without touching the frozen `PageFactory` | `d052269` |
| NAV-009 surface DPI and ergonomics dispositions | All three disposed "correct": side-effect-free `Scale`, virtual `Dispose()` on the WPF surface bases, a real page z-order band on WPF | `1283f58` |
| NAV-010 anchored surface placement and Toolkit wiring | The layered host also implements `INavigationToolkit`; bootstrap registers it through a probe; the WinForms toast parks itself at `BottomRight` | `03f5760`, closed `70ef4eb` |
| NAV-011 idle watchdog disarmed after a successful tick | Rearm after every completed tick, and skip a tick entirely when already idle and already signed out | `620601a` |

Two findings did not come from the code-first review and are recorded here for
completeness: **NAV-001** came from the PCB emulation scenario on 2026-08-02,
and **NAV-002** from an external NuGet consumer scenario on 2026-08-03. Both were
confirmed against current source before promotion.

## Rejected alternatives

Preserved so they are not re-proposed.

- **NAV-010 — a `CreateNavigationToolkit` factory on `IPlatformAdapter`.**
  Rejected: it breaks every third-party adapter implementation, for no gain over
  a `host as INavigationToolkit` probe that mirrors the existing `IViewHost` one.
- **NAV-007 — a real click-outside model.** Rejected: mouse capture or a hit-test
  scrim is a materially different interaction model and is explicitly not
  accepted by that entry. The focus-driven contract was documented instead.
- **NAV-011 — a bare "always rearm".** Rejected: `NavigationSession.SignOut()`
  raises `Changed` on every call and the idle page is `Transient` by default, so
  an always-armed timer would re-run the whole transition and dispose and
  recreate the idle page every interval for as long as the terminal stayed
  unattended.
- **NAV-011 — rearming only when not on the idle page.** Rejected: it leaves the
  same hole, because once the timer is disarmed while idle, no tick ever comes to
  observe that the shell moved away.
- **NAV-009(a) — keeping `CreateGraphics()` and documenting the side effect.**
  Rejected: realizing a window as a consequence of reading a scale factor is not
  a documentable behavior.
- **NAV-009(b) — mirroring the full WinForms `protected virtual void
  Dispose(bool)` pattern.** Rejected: it adds a protected member to four public
  types to express a finalizer contract none of them has.
- **NAV-009(c) — keep-and-document the WPF z-order collapse.** Rejected: the
  divergence is only masked because hidden keep-attached pages are collapsed, and
  would resurface the moment two pages are visible at once.

## Evidence classification

Phase E2 requires automated, build-only, and interactive evidence to be
distinguished rather than pooled. As reconciled at `0449e79`:

| Evidence | What it covers |
|---|---|
| **Automated** | The Navigation unit suite, **267 passing on `net481` and on `net9.0-windows`**, 0 failed, 0 skipped — up from 222 at the review baseline. Every behavioral fix added dual-target regressions, and for each one the new regressions were run against the *old* implementation and confirmed to fail before the fix was trusted. The discriminating symptom is recorded in each commit message. |
| **Build-only** | The whole solution builds on both target families; both smoke scenarios build; `eng/verify-docs.ps1` passes; no new warning identity was introduced. |
| **Interactive** | NAV-003, NAV-004 and NAV-005 were closed on hand-driven smoke runs on 2026-08-03. NAV-007 and NAV-010 were closed on hand-driven runs of the toast and popover steps — the WinForms scenario on **both** `net9.0-windows` and `net481`, the WPF scenario on `net9.0-windows`. NAV-011's reproduction was timed by hand. Per-region dismissal results are recorded in `TODO.md` and in each scenario's README verification record. |

Three discrimination passes failed to **compile** rather than failing an
assertion, because the regression used a member the fix introduced — CS0506 for
NAV-009(b), CS0115 for NAV-010's `AnchorInset`, CS1061 for the toolkit
`Surface`. That is still proof, and the compiler names the exact missing member,
but it hides every other result in the same pass; those were proven separately.

## Residual gaps

Stated rather than closed, so no later reader mistakes them for verified.

- **NAV-001's native repeat was performed on 2026-08-04** and the item is closed.
  It could not be reproduced from a smoke scenario — there is no public
  session-changed event — so it was driven in the originating application,
  `NekoPcbMiddleware`, against the packaged `1.0.0-local.8`. Two interactive
  anonymous idle transitions navigated to the idle page, and the app's own
  `S16.1` scenario confirmed both halves on an authenticated session: signed out
  and navigated to `HomePage`. **That repeat is confirmation, not
  discrimination** — the original symptom was intermittent, and `S16.1` was
  recorded as passing against `1.0.0-local.7` on the same day the symptom was
  first seen. The discrimination rests on the `PageNavBootstrapLifetimeTests`
  regression that was confirmed to fail against the previous implementation.
- **No public session-changed event.** `NavigationSession.Changed` is `internal`
  and its only subscriber is the Inspection observer. Found while implementing
  NAV-001; deliberately not promoted, and recorded as a candidate.
- **The WPF toast case with a handled mouse event was not exercised.** Neither
  scenario's toast contains a `Button`, so "a child that marks the event handled
  does not dismiss" rests on framework semantics plus source, not on an
  observation. Adding a button to the WPF sample toast would close it, and would
  double as the ready-made close-button toast proposal NAV-007 mentions.
- **The WPF smoke scenario is `net9.0-windows` only**, while the WPF adapter also
  targets `net481`, so NAV-010's "both target families" is satisfied on the
  WinForms side only. Whether to multi-target that scenario is an open question.
- **Both scenarios are now walked end to end** (2026-08-04): the WinForms
  procedure on `net481`, the WPF procedure on `net9.0-windows`. Two steps proved
  **not performable as written**, and both are procedure defects rather than
  adapter defects. WinForms step 6 wants a popover alive while a prompt is
  opened, but every control that opens the prompt takes focus, so the
  auto-dismiss popover resolves first — correct NAV-007 behaviour. WPF step 4
  wants an anonymous attempt at a guarded page denied, but no page in that
  scenario declares a guard. The behaviour behind both is covered automatically
  on both target families — by
  `WinFormsInteractionBlocker_LateViewsAndModalStack_RestoreStates` and its WPF
  twin, and by `RequireAuthenticatedGuardTests` — and guard denial was
  additionally demonstrated end to end in a real consumer.
- **Interactive coverage is uneven across target families.** The WinForms
  procedure was walked in full on `net481` but only steps 4 and 5 on
  `net9.0-windows`; the WPF scenario is `net9.0-windows` only, so the WPF adapter
  has no interactive evidence on `net481`. Every adapter behaviour has automated
  coverage on both families, and where the two were compared interactively they
  behaved identically, so this is missing demonstration rather than unknown
  behaviour.
- **The scenarios do not resemble a real application.** They exercise one control
  of each kind in a single window. Nothing here was driven under dense pages,
  competing surfaces, sustained operator input, or hardware in the loop. The
  adapters are reviewed and corrected, not exercised under realistic load; E3
  owns long-running and recovery scenarios.
- **`WpfEventDispatcherAdapter` answers the unreachable-UI-thread question
  differently** from the corrected WinForms one: it runs the action inline from
  any thread once its `Dispatcher` reports shutdown. NAV-006 recorded the
  divergence in the contract documentation rather than changing WPF, because that
  item is scoped to the WinForms adapter. A candidate, not an accepted task.

## Public-surface changes

Recorded under F1 in `TODO.md` as inputs to the future breaking-change policy,
not as F1 work.

- Two dead public WPF types removed: `InteractionObserver` and
  `EventSubscriptionAdapter` (NAV-008c).
- `IPageView.Name` seeding on the WinForms base classes changed from
  `GetType().FullName` to `GetType().Name` (NAV-008g).
- `Dispose()` became `virtual` on the four WPF surface bases — source-compatible,
  binary-breaking (NAV-009b).

## Boundaries preserved

`NavigationContext`, `NavigationRuntime`, `PageRegistry`, and `PageFactory` were
not modified by any of the eleven findings. NAV-008(d) came closest and
deliberately gave `PageFactory.Warn` a consumer from `PageNavBootstrap` instead.
The canonical lifecycle order, the navigation-gate behavior, the overlay teardown
asymmetry, and the static-facade semantics are unchanged. Phase F remains gated.
