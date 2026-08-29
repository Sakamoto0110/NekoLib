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

The ordinary route is two lines. Reference the `NekoLib.Watchdog` package and
the `NekoLib.Watchdog.Host` deployment package from your executable, then call
the bootstrap near the start of `Main`:

```csharp
static void Main(string[] args)
{
    WatchdogBootstrap.EnsureStarted(args);
    // normal application startup
}
```

The call is synchronous and bounded. It starts the deployed sidecar, hands over
the current process, and returns; a replacement started later by the sidecar
sees `NEKO_UNDER_WATCHDOG=1` and returns immediately instead of starting a
second Host. From then on `WatchdogController` lets the application ping, read
status, pause, resume, restart, stop, subscribe to the log stream, and report an
unhandled exception. A custom supervisor process can instead host
`WatchdogRuntime` directly — a deliberate advanced surface, not Host-only
infrastructure.

Three boundaries matter before adopting it. **Control is not authorized:**
endpoints are `CurrentUserOnly`, which stops other users but not a hostile
process already running as the same user, and any such process can stop or
restart your application. **Diagnostics are bounded and best effort:** the
replay history, the event queue, and the forwarding sink all drop under
pressure, and the separate counters in `Status()` are how you find out.
**Deployment is a separate contract:** sidecar payloads, RID selection, and the
launch protocol belong to `NekoLib.Watchdog.Host`, and the two packages must be
upgraded together.

Start with the [technical reference](REFERENCE.md) for the complete
composition, configuration, lifecycle, control, evidence, and security contract.
Use the [module manifest](MANIFEST.md) for packages, targets, API baselines,
tests, scenarios, audits, and migration routes. Consumers upgrading from a
pre-`1.0.0` candidate should read the [F1-WDOG migration](migrations/f1.md)
first.
