# Navigation WPF Smoke Scenario

**Kind:** guide

**Lifecycle:** current

**Owner:** Navigation WPF adapter

**OS / target:** Windows, `net9.0-windows`

**Prerequisites:** .NET 9 SDK and Desktop Runtime; interactive desktop session

**Last verification:** partial interactive run on 2026-08-03 at `822b51b` —
steps 2 and 5 driven by hand and passing; the remaining steps were not driven

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
   remain non-modal and close on focus loss.
4. Sign out and attempt the guarded page, then sign in as `admin` and retry. The
   anonymous attempt must be denied and the authenticated attempt allowed.
5. Exercise the lifecycle controls, in order. Open a Popover, then press
   **Reset (ResetAsync)**: the popover must close, its awaiter must resolve
   (`Popover -> False` in the log), the current page must clear, history must
   report `CanGoBack=False`, and navigation must still work afterwards. Press
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
