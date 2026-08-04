# Navigation WPF Smoke Scenario

**Kind:** guide

**Lifecycle:** current

**Owner:** Navigation WPF adapter

**OS / target:** Windows, `net9.0-windows`

**Prerequisites:** .NET 9 SDK and Desktop Runtime; interactive desktop session

**Last verification:** 2026-08-04 at `7e26b87` — steps 1, 2, 3, 5, 6 and 7
driven by hand and passing; step 4 is not performable because the scenario
declares no guarded page

## Purpose

Exercise real WPF hosting for page switching, history, idle navigation, session
guards, dialog/prompt/toast/popover behavior, reset, and shutdown. Headless unit
tests remain the automated contract; this scenario checks native visual and
interaction behavior they cannot observe.

## Build

```powershell
dotnet build runtime_tests/Navigation/WpfSmoke/NekoLib.Navigation.RuntimeTests.WpfSmoke/NekoLib.Navigation.RuntimeTests.WpfSmoke.csproj
```

## Launch

```powershell
dotnet run --project runtime_tests/Navigation/WpfSmoke/NekoLib.Navigation.RuntimeTests.WpfSmoke/NekoLib.Navigation.RuntimeTests.WpfSmoke.csproj
```

## Procedure and expected result

1. Open Dashboard, return to Idle, and use Back. The visible page and event log
   must agree with each action.
2. Open Dialog and Prompt. Each must appear as a centered bounded surface, block
   background interaction, and return the selected result.
3. Open Toast and Popover. Toast must dismiss after its duration; Popover must
   remain non-modal and close on focus loss. Light dismissal follows **focus, not
   hit testing**: clicking a control that can take focus dismisses the popover,
   clicking inert area does not. See NAV-007.
4. Sign out and attempt the guarded page, then sign in as `admin` and retry. The
   anonymous attempt must be denied and the authenticated attempt allowed.
5. Exercise the lifecycle controls, in order. Open a Popover, then press
   **Reset (ResetAsync)**: the popover must close, its awaiter must resolve
   (`Popover -> False` in the log), history must report `CanGoBack=False`, and the
   shell must land back on Idle. `ResetAsync` itself does not navigate — it clears
   the shell and leaves the context alive — so the scenario goes to Idle right
   after it, which is what a real shell would do. Press
   **Shutdown**: the facade unmounts, and any further navigation must log an
   error rather than crash. Press **Start (re-bootstrap)**: a fresh context must
   mount and navigation must work again. Repeat the Shutdown/Start pair a few
   times — repeated mount and shutdown is explicit E2 scope.
6. Leave the Dashboard idle for the displayed timeout. The session must sign out
   and Navigation must return to the Idle page.
7. Close the window. No modal awaiter may remain hung and the window must close
   without a navigation teardown deadlock.

## Cleanup and side effects

The scenario creates no persistent application data and starts no child
process. Close the window after the checks. `bin/` and `obj/` are disposable
build output.

## Verification record

- 2026-08-01 / `32fc67e`: project built successfully on Windows for
  `net9.0-windows`; interactive behavior was not claimed.
- 2026-08-03 / `ae17810`: rebuilt successfully on Windows for `net9.0-windows`
  with 0 warnings and 0 errors during the Phase E2 adapter review; interactive
  behavior was again not claimed.
- 2026-08-03 / `822b51b`: **partial interactive pass** on `net9.0-windows`.
  Step 2 passed, including the NAV-003 check — with the dialog open and the mouse
  untouched, Space activated the focused Confirm button and logged
  `Dialog -> True` — and the NAV-005 no-regression check: clicking the page behind
  an open modal left focus in the modal, and the Dashboard counter worked again
  after it closed. Step 5 passed, repeated several times with overlays open:
  Reset, Shutdown, and Start all behaved, and `Start` correctly refuses to run
  until `Shutdown` has completed. Steps 1, 3, 4, 6 and 7 were **not** driven.
  The run also exposed an unrelated idle defect; see the idle finding in
  [`TODO.md`](../../../TODO.md).
- 2026-08-03 / `03f5760`: **step 3 driven by hand** on `net9.0-windows`, closing
  the WPF halves of NAV-007 and NAV-010.
  - **Toast.** It appeared parked at the host's bottom-right corner at its own
    size. Clicking the message text **dismissed** it: the toast opened at
    23:57:27.252 and was gone roughly 0.8 s later, well inside its 3 s duration,
    so the click and not the timer closed it. This is the documented divergence
    from WinForms, where the same click dismisses nothing.
  - **Popover.** It opened with its field focused. Tab moved focus to the Fechar
    button without dismissing. Clicking the inert Idle page — labels only — left
    it open, confirming that light dismissal follows focus and not hit testing on
    WPF too. Clicking `SignIn("admin")`, a focusable control outside the host,
    dismissed it with `Popover -> False`.
  - **Not covered:** a toast containing a control that marks the mouse event
    handled, such as a `Button`. This scenario's toast holds only a `TextBlock`,
    so that half of NAV-007's WPF row still rests on framework semantics rather
    than on an observation. Closing the window exited the process cleanly.
- 2026-08-04 / `7e26b87`: **steps 1, 6 and 7 driven by hand**, and step 4 found
  to be not performable.
  - Step 1: Dashboard → Idle → Back all navigated, `GoBack -> True`, with
    `HistoryChanged: CanGoBack` and `CurrentChanged` tracking each move.
  - Step 6: with the Dashboard counter clicked to a confirmed `Cliques: 1` — so
    the interaction genuinely landed inside the host — idle fired **exactly 20 s
    later**, at 14:43:35 → 14:43:55, navigating `DashboardPage -> IdlePage`
    against the declared `[PageTimeout(20)]`.
  - Step 7: closing the window exited the process cleanly, with empty stderr and
    no hung awaiter.
  - **Step 4 cannot be performed: this scenario has no guarded page.** The
    procedure asks for an anonymous attempt at a guarded page to be denied and an
    authenticated one allowed, but no page in the WPF scenario declares any guard
    attribute — only the `SignIn("admin")`/`SignOut` buttons and a `GuardDenied`
    log subscription exist. Both were exercised and `SignIn(admin) — auth=True`
    was logged, but no denial can occur. Either add a guarded page to the
    scenario or correct the step.
