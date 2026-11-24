# Modal binding correction for desktop readiness

**Kind:** reference

**Lifecycle:** current

**Subject:** accepted compatible patch for modal ownership across enabled-state binding refreshes

**Decision date:** 2026-11-24

The [1.2 desktop integration contract](desktop-readiness-1.2.0.md) remains the
API and native-semantics foundation. A supplemental regression after the 1.2.0
NuGet upload found that refreshing an enabled-state binding from false to true
could re-enable a background view while a modal still owned interaction. The
same behavior was reproduced through the signed, published 1.2.0 WPF and
WinForms packages. The original four-way probes passed but did not contain
this additional binding-refresh case.

The owner-authorized desktop scope reopens only the two native interaction
blockers and their qualification probes for this correction. Version 1.2.1 is
a coordinated patch: it restores the existing contract that background views
remain disabled and only the top modal is interactive. It changes no public
API, dependency, target, Host protocol, or accepted API manifest. Published
1.2.0 artifacts remain immutable and are superseded for desktop readiness.

## Corrected behavior and ownership

- Both adapters track enabled-state changes for the views owned by their modal
  scope. A background enable request is held until that scope releases it;
  source refresh cannot enable an earlier modal or page.
- WPF retains the existing binding expression with `SetCurrentValue`, restores
  the current binding target on release, and removes its handlers on removal
  or final unblock. Initially disabled bound pages can become enabled after
  release when their source has changed.
- WinForms retains the tracked view's Enabled binding, suspends that binding's
  target-to-source writes during modal ownership, refreshes the current source
  value on release, and restores its original `DataSourceUpdateMode`. Replacing
  an Enabled binding during the scope also restores both bindings' modes.
- Bind enabled state as a view-model output: WPF's default one-way IsEnabled
  binding and WinForms `DataSourceUpdateMode.Never` give the portable application
  policy. Navigation owns the tracked native enabled property during a modal;
  application business state belongs in the model, not competing native writes.

Nested modal ownership, removed-view cleanup and legacy constructors remain
unchanged. The single-window application scope, application-owned synchronization
rules, native toast semantics, Watchdog process-existence heartbeat and explicit
manual visual/soak limitations from the 1.2 decision remain in force.

## Evidence

The negative source check is retained in
`artifacts/e2e-readiness/modal-source-refresh.log`. Package-only negative runs
against downloaded 1.2.0 are in `modal-negative-published-WinForms9.log` and
`modal-negative-published-Wpf9.log`. Focused corrections cover active native
WinForms bindings across all three update modes, source changes on release,
binding replacement, initially disabled WPF pages and nested WPF modals. The
complete Navigation suite passes 312 tests on each target.

The [final stable release record](stable-release-1.2.1.md) identifies the clean
patch source, full family qualification, new immutable package hashes,
publication and independent downloaded-package qualification. Those separate
stages supply the final delivery evidence beyond the focused regression result.
