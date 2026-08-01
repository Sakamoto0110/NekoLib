# Navigation WPF Smoke Scenario

**Kind:** guide

**Lifecycle:** current

**Owner:** Navigation WPF adapter

**OS / target:** Windows, `net9.0-windows`

**Prerequisites:** .NET 9 SDK and Desktop Runtime; interactive desktop session

**Last verification:** build-only on 2026-08-01 at scenario-source commit
`32fc67e`; interactive procedure not rerun

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
5. Exercise reset/shutdown controls. No modal awaiter may remain hung and the
   window must close without a navigation teardown deadlock.
6. Leave the Dashboard idle for the displayed timeout. The session must sign out
   and Navigation must return to the Idle page.

## Cleanup and side effects

The scenario creates no persistent application data and starts no child
process. Close the window after the checks. `bin/` and `obj/` are disposable
build output.

## Verification record

- 2026-08-01 / `32fc67e`: project built successfully on Windows for
  `net9.0-windows`; interactive behavior was not claimed.
