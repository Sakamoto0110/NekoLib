# NekoLib.Navigation Confirmed Issues

**Document ID:** NAV-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Navigation family boundary

**Surface:** issues

**Boundary:** navigation

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

## NAV-ISSUE-001

**Status:** fixed

**Severity:** low

**Affected releases:** observed in 1.0.0 and the 1.4.0 line

**Symptom:** Accessing `NavigationService.Session`, `History`, `CanGoBack`, or `Events` before bootstrap or after shutdown raised an `InvalidOperationException` that directed the consumer to the internal `NavigationService.UseContext` method.

**Trigger:** Read any of the four mounted-context properties while no context is mounted.

**Evidence:** `ContextProperties_BeforeStartAndAfterShutdown_ReportPublicBootstrapGuidance` exercises all four properties on both sides of a public bootstrap lifecycle.

**Workaround:** Call `PageNavBootstrap.Start()` before reading mounted-context properties and do not read them after `NavigationService.Shutdown()` until a fresh context is mounted.

**Intended fix:** Align the public error text with `PageNavBootstrap.Start()` and the awaited-shutdown contract without changing the exception type or facade lifecycle.

**Fix release:** planned for 2.0.0, unreleased

**Roadmap:** tracked in the [family changelog](../../../CHANGELOG.md#unreleased).

## NAV-ISSUE-002

**Status:** fixed

**Severity:** The user can lose the active page immediately after selecting it.

**Affected releases:** 2.0.0 local candidate at `b418aa69184b1006f006f13f114f3951c1597db7`. Owned by the shared Navigation core. The measured reproduction used WinForms on net481.

**Symptom:** Selecting Dashboard from the sidebar returned the user to Idle about 0.51 seconds later despite a configured 20-second inactivity interval.

**Trigger:** The sidebar is outside the registered interaction host. Selecting a page there near the existing deadline changes the page while the old idle interval continues.

**Evidence:** The candidate trace recorded Dashboard at 07:03:24.461 UTC and Idle at 07:03:24.973 UTC. A click inside Dashboard reset the timer and Idle followed 20.010 seconds later. Local qualification evidence: `artifacts/release-qualification-b418aa6/nav-idle-probe/REPORT.md` and its `trace.log`.

**Workaround:** Interaction inside the observed host renewed the interval in the affected candidate.

**Intended fix:** Report user input outside the observed host through `NavigationService.NotifyUserInteraction()` before requesting navigation. The runtime ignores early ticks and rechecks for newer activity as the automatic sign-out enters the navigation gate. See the [idle contract](REFERENCE.md#idle-system).

**Verification:** `IdleDeadline_OnlyUserInputRenewsItAndInvalidatesStaleTicks` and `IdleDeadline_UserInputWhileTimeoutWaitsForNavigationGateCancelsSignOut` passed on `net481` and `net9.0-windows`. The package-only WinForms probe selected Dashboard just before the old deadline. The renewed deadline expired 20,019 ms after the sidebar click on `net481` and 20,017 ms on `net9.0-windows`. Local evidence is under `artifacts/navigation-idle-regressions/` in `final-unit.log` and both `consumer-*.log` files. [Validation requirement](VALIDATION_REQUIREMENTS.md#nav-valreq-003)

**Fix release:** planned for 2.0.0, unreleased.

**Roadmap:** Local candidate validation is recorded above. [2.0.0 release notes](../../release-notes-2.0.0.md#other-consumer-visible-changes)

## NAV-ISSUE-003

**Status:** fixed

**Severity:** Security release blocker. At a shared terminal the next user could see session-specific content left in a popover. The reproduction observed the visible surface using test content.

**Affected releases:** 2.0.0 local candidate at `b418aa69184b1006f006f13f114f3951c1597db7`. Owned by the shared Navigation core. The measured reproduction used WinForms on net481.

**Symptom:** Automatic sign-out navigated to Idle while the Dashboard popover remained attached and visible, with its result task still pending.

**Trigger:** Open the popover on Dashboard and let inactivity expire while the popover remains open.

**Evidence:** The operator log recorded Idle at 04:03:07.814 and the popover result 6.267 seconds later. The dismissal trigger in that run is unknown. An isolated reproduction observed the popover still attached, visible and pending after Idle. An explicit Reset then closed it and completed its result with `false`. Local qualification evidence: `artifacts/release-qualification-b418aa6/probes/nav-popover-observed.txt` and `artifacts/release-qualification-b418aa6/nav-idle-probe/REPORT.md`.

**Workaround:** Explicit `ResetAsync()` dismissed the retained popover and returned `false` in the affected candidate.

**Intended fix:** Automatic sign-out detaches and disposes open popovers before presenting Idle and settles pending results with `false`, including when a popover is still initializing.

**Verification:** `AutomaticIdle_DetachesAndCancelsPreviousSessionPopoverBeforePresentingIdle` passed on `net481` and `net9.0-windows`. The package-only WinForms probe confirmed the old popover was gone at the first signed-out Idle presentation, returned `false`, and did not return after the next sign-in on either target. Local evidence is under `artifacts/navigation-idle-regressions/` in `final-unit.log` and both `consumer-*.log` files. [Validation requirement](VALIDATION_REQUIREMENTS.md#nav-valreq-003)

**Fix release:** planned for 2.0.0, unreleased.

**Roadmap:** Local candidate validation is recorded above. [2.0.0 release notes](../../release-notes-2.0.0.md#other-consumer-visible-changes)

## NAV-ISSUE-004

**Status:** fixed

**Severity:** Session-specific input could remain visible and the modal could block Idle.

**Affected releases:** The local `2.0.0-idlefix.1` candidate kept prompts attached after automatic sign-out. The WinForms reproduction on both target families found the old text still in an enabled prompt, its result pending, and the Idle page blocked by the modal.

**Symptom:** The original prompt and its pending result outlived the first Idle presentation.

**Trigger:** Navigate to Idle or let the inactivity deadline expire with an open modal prompt.

**Evidence:** The package-only WinForms reproduction recorded `attached=True`, `enabled=True`, `resultPending=True`, and `idleEnabled=False` on both targets. See `artifacts/navigation-idle-regressions/prompt-net481.log` and its .NET 9 counterpart.

**Workaround:** Explicit `ResetAsync()` removed the retained prompt and completed the pending text result with `null` in the affected candidate.

**Intended fix:** Session sign-out detaches and disposes open prompts and releases their modal block. Navigation to Idle also closes prompts and waits for their results before presentation. Direct `GoIdleAsync()` preserves the session. A dismissed text prompt returns `null`. Prompts that implement `IPromptDraftState` can keep a draft for the next opening on the same page in the same session. [Idle contract](REFERENCE.md#idle-system)

**Verification:** `AutomaticSignOut_CancelsPromptBeforeIdleIsPresented`, `NavigateToIdle_CancelsPromptBeforePresentationAndPreservesSession`, `ExplicitSignOut_CancelsPromptAndKeepsTheCurrentPage`, and `IdlePromptDraft_RefillsTheNextPromptOnlyOnItsOriginPageAndSession` passed on `net481` and `net9.0-windows`. Package-only WinForms probes on both targets confirmed clean Idle presentation, draft restoration on the next opening, confirmation through the new result, and draft removal on automatic sign-out. Local evidence is under `artifacts/navigation-idle-prompt-draft/` in `final-unit.log` and both `consumer-*.log` files. [Validation requirement](VALIDATION_REQUIREMENTS.md#nav-valreq-003)

**Fix release:** planned for 2.0.0, unreleased.

**Roadmap:** Local candidate validation is recorded above. [2.0.0 release notes](../../release-notes-2.0.0.md#other-consumer-visible-changes)
