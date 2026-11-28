# NekoLib.Watchdog

**Document ID:** WDG-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to NekoLib.Watchdog

**Surface:** introduction

**Boundary:** watchdog

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Watchdog keeps one unattended Windows application running. It supervises
a single target executable on `net481` and `net9.0-windows`: it attaches to the
running instance or launches one, replaces it after a crash, exposes a small
control channel over named pipes, forwards operational logs, and finalizes crash
evidence into retained bundles.

For a C# WinForms or WPF executable, install `NekoLib.Watchdog` and explicitly
enable automatic startup in the executable's project file. Set the MSBuild
property `NekoLibFamilyVersion` in your project or `Directory.Build.props` to
the exact family version in use. For a local 2.0 candidate, use the full
candidate package version for every NekoLib reference.

```xml
<PropertyGroup>
  <NekoLibWatchdogAutoBootstrap>true</NekoLibWatchdogAutoBootstrap>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="NekoLib.Watchdog" Version="$(NekoLibFamilyVersion)" />
</ItemGroup>
```

Rebuild and launch the application's `.exe`. Package installation alone does
not activate supervision. The default starts a second, headless process of the
same executable; no extra application window, Host package or application Main
call is needed. The [automatic startup contract](REFERENCE.md#automatic-application-startup)
lists supported projects, compiler requirements and launch diagnostics.

Use the public API and this module's reference to integrate a consumer; reading
Watchdog implementation source is not an integration prerequisite. Optional
configuration belongs before activation, in a WinForms form constructor or WPF
OnStartup. See the [copyable examples](REFERENCE.md#optional-startup-configuration)
and [UI-hang policy](REFERENCE.md#ui-hang-recovery) for timeout/grace defaults.

The application can access these static controls from any component:

| Operation | Public call |
|---|---|
| Disable supervision and keep the application running | `WatchdogController.Disable()` |
| Enable supervision again | `WatchdogController.Enable()`; check its Boolean result |
| Disable supervision and exit the application | `WatchdogController.Shutdown(exitCode)`; finish application cleanup first |
| Temporarily pause or resume automatic recovery | `WatchdogController.Pause()` / `Resume()` |
| Inspect the supervisor | `WatchdogController.Ping()` / `Status()` |

These calls are synchronous. The [control contract](REFERENCE.md#control-contract)
owns failures, threading and completion guarantees. Runtime reconfiguration uses
[Disable, Configure, then Enable](REFERENCE.md#global-controls-and-runtime-reconfiguration).

The [optional sidecar](REFERENCE.md#manual-sidecar-startup) and
[custom supervisor runtime](REFERENCE.md#custom-supervisor-runtime) remain
supported compositions. Sidecar deployment needs a direct same-version
`NekoLib.Watchdog.Host` reference. A custom supervisor uses public
`WatchdogRuntime`; reliable recovery still requires a separate process.

Three boundaries matter before adopting it. **Control is not authorized:**
endpoints are `CurrentUserOnly`, which stops other users but not a hostile
process already running as the same user, and any such process can stop or
restart your application. **Diagnostics are bounded and best effort:** the
replay history, the event queue, and the forwarding sink all drop under
pressure, and the separate counters in `Status()` are how you find out.
**Sidecar deployment has its own contract:** when selecting Sidecar, its
payloads, RID selection, and launch protocol belong to `NekoLib.Watchdog.Host`.
Upgrade Host and the managed library together in that mode. SameExecutable
uses the application's own executable for both process roles.

Start with the [technical reference](REFERENCE.md) for the complete
composition, configuration, lifecycle, control, evidence, and security contract.
Use the [module manifest](MANIFEST.md) for packages, targets, API baselines,
tests, scenarios, audits, and migration routes. Consumers upgrading from a
pre-`1.0.0` candidate should read the [F1-WDOG migration](migrations/f1.md)
first.
