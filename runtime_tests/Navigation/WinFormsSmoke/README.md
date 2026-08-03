# Navigation WinForms Smoke Scenario

**Kind:** guide

**Lifecycle:** current

**Owner:** Navigation WinForms adapter

**OS / target:** Windows, `net481` and `net9.0-windows`

**Prerequisites:** .NET 9 SDK, .NET Framework 4.8.1 targeting pack, .NET Desktop
Runtime; interactive desktop session

**Last verification:** build-only on 2026-08-03 against repository baseline
`ae1781086b3858cdc9cb025473ed18e3445ee1eb`, with the scenario added in the
working tree; the interactive procedure has not been performed

## Purpose

Exercise real WinForms hosting for page switching, history, idle navigation,
session guards, dialog/prompt/toast/popover behavior, the interaction blocker,
and shutdown. It is the WinForms counterpart of the
[WPF smoke scenario](../WpfSmoke/README.md) and deliberately uses the same
controls, labels, and layout so the two platforms can be compared step by step.

Headless unit tests remain the automated contract. Only eight tests in
`NekoLib.Navigation.Tests.Unit` touch a native adapter at all, and none of them
covers dispatch, focus, the interaction observer, the timer, or any view base —
this scenario is where that native behavior becomes observable.

The left-hand control column sits **outside** the navigation host. That is
intentional and has two consequences worth knowing before interpreting results:
its buttons stay interactive while a modal blocks the host, and clicking them
does **not** reset the idle timer, because the interaction observer only watches
the host subtree.

## Build

```powershell
dotnet build runtime_tests/Navigation/WinFormsSmoke/NekoLib.Navigation.RuntimeTests.WinFormsSmoke/NekoLib.Navigation.RuntimeTests.WinFormsSmoke.csproj
```

## Launch

Pick a target family explicitly; the project multi-targets.

```powershell
dotnet run --project runtime_tests/Navigation/WinFormsSmoke/NekoLib.Navigation.RuntimeTests.WinFormsSmoke/NekoLib.Navigation.RuntimeTests.WinFormsSmoke.csproj -f net9.0-windows
```

```powershell
dotnet run --project runtime_tests/Navigation/WinFormsSmoke/NekoLib.Navigation.RuntimeTests.WinFormsSmoke/NekoLib.Navigation.RuntimeTests.WinFormsSmoke.csproj -f net481
```

Run the procedure once per target family and record each result separately.

## Procedure and expected result

1. **Page switching and history.** Open Dashboard, return to Idle, then use
   Back. The visible page and the event log must agree with each action, and
   `HistoryChanged: CanGoBack=` must track the back stack.
2. **Modal surfaces.** Open Dialog and Prompt. Each must appear as a centered
   bounded box — not full screen — and must return the selected result. While
   either is open, the Dashboard counter button must be disabled and the
   dialog's own buttons must stay live.
3. **Prompt keyboard focus.** Open Prompt and type immediately, without clicking
   the field first. The text must land in the input, because the WinForms host
   forwards focus to the surface's first selectable child. Note that Enter and
   Escape are **not** wired: a WinForms surface is a `UserControl`, not a `Form`,
   so it has no `AcceptButton`/`CancelButton` equivalent. Close with the buttons.
4. **Toast.** Open Toast. It must appear bottom-right at its own size and
   disappear after 3s. Then open another Toast and click it — click the dark
   background area, then repeat and click directly on the message text. Record
   which of the two dismisses the toast; see NAV-007.
5. **Popover focus dismissal.** Open Popover. Its field must already have focus.
   Tab between the field and the Fechar button — the popover must **not** close.
   Then click a control outside the popover: per the documented contract the
   popover must close. **This step is the one under investigation** — the E2
   review predicts it will not close, because the WinForms focus observer
   watches the container rather than the surface subtree. Record what actually
   happens; see NAV-004.
6. **Popover interaction while a modal is open.** With a Popover open, open a
   Prompt. The popover must become disabled while the prompt stays interactive,
   and must become interactive again when the prompt closes.
7. **Idle timeout.** Leave the pointer and keyboard away from the navigation
   host for 20s while on Dashboard. The session must sign out and Navigation
   must return to the Idle page. Clicking inside the host — for example the
   Dashboard counter — must restart the interval; clicking the left-hand panel
   must not.
8. **Session guards.** Use SignIn("admin") and SignOut and confirm the logged
   authentication state changes.
9. **Reset, shutdown, and repeated mounting.** Open a Popover, then press
   **Reset (ResetAsync)**: the popover must close, its awaiter must resolve
   (`Popover -> False` in the log), the current page must clear, history must
   report `CanGoBack=False`, and navigation must still work afterwards. Press
   **Shutdown**: the facade unmounts, and any further navigation must log an
   error rather than crash. Press **Start (re-bootstrap)**: a fresh context must
   mount and navigation must work again. Repeat the Shutdown/Start pair a few
   times — repeated mount and shutdown is explicit E2 scope.
10. **Window close.** Close the window. `NavigationService.Shutdown()` runs from
    `FormClosing`, while the message pump is still alive. No modal awaiter may
    remain hung, and the process must exit without a navigation teardown
    deadlock.

## Known open findings this scenario exercises

Read these before recording a result, so an expected failure is not logged as a
new discovery. All are tracked in [`TODO.md`](../../../TODO.md) under Phase E2.

| Item | What to watch |
|---|---|
| NAV-004 | Step 5 — popover auto-dismissal on focus loss is predicted not to fire |
| NAV-007 | Step 4 — which regions of a toast actually dismiss it |
| NAV-010 | Step 4 — the toast is positioned by the scenario, not by `ToastViewBase` |
| NAV-001 | Step 7 — an idle tick must not be cancelled by sign-out UI updates |
| NAV-006 | Step 10 — teardown when the host handle is gone |
| NAV-008(f) | Step 9 — the unmounted-facade error names a non-existent `Initialize` |

## Cleanup and side effects

The scenario creates no persistent application data, writes no files, and starts
no child process. Close the window when finished. `bin/` and `obj/` are
disposable build output.

## Verification record

- 2026-08-03 / baseline `ae17810`, scenario in working tree: project built
  successfully on Windows for `net481` and `net9.0-windows`, 0 warnings and
  0 errors. **Build-only.**
- 2026-08-03: an automated harness outside this scenario drove the form through
  bootstrap, idle navigation, dialog, popover, toast, Dashboard navigation,
  prompt, and shutdown without error, and confirmed the surface geometry
  (centered dialog, top-left popover, bottom-right toast) and the blocker
  disabling background views. A second pass drove Reset → Shutdown → Start:
  `ResetAsync` resolved the open popover's awaiter with `false` and cleared
  current/history, navigation after Shutdown logged an error instead of
  crashing, and Start remounted a working context. This is **not** interactive
  verification: no person performed the procedure above, and it did not exercise
  real mouse focus transitions, which is exactly what steps 4, 5 and 7 depend
  on.
- The interactive procedure has not been performed on either target family.
