# NekoLib.Watchdog.Host

**Document ID:** WDGHOST-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to NekoLib.Watchdog.Host

**Surface:** introduction

**Boundary:** watchdog.host

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

`NekoLib.Watchdog.Host` deploys the Watchdog sidecar next to your application.
It is a deployment package, not a library: it exports no compile-time API, and
its contract is the payload it deploys and the protocol it speaks.

Reference it directly from every executable that should carry a sidecar, then
call `WatchdogBootstrap.EnsureStarted` from the `NekoLib.Watchdog` library. The
package copies the payload matching your target and architecture into an owned
`NekoLib.Watchdog.Host` directory beside your output on build and publish.

Start with the [technical reference](REFERENCE.md) for payload layout, RID
selection, the consumer properties, protocol v1, exit codes, and fatal evidence.
Use the [module manifest](MANIFEST.md) for the project, package, tests,
scenarios, audits, and migration routes.

Two things surprise consumers most. **Deployment does not propagate:** a wrapper
library that depends on this package gives its downstream executables nothing,
by design — each executable declares its own reference and owns its architecture
choice. **Versions are coordinated:** the Host and `NekoLib.Watchdog` speak an
internal protocol, so upgrade both together and rebuild, or bootstrap fails with
an explicit protocol-mismatch error.
