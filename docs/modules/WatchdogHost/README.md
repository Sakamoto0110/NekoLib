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

`NekoLib.Watchdog.Host` is the optional deployment package for Sidecar
supervision. Automatic startup defaults to SameExecutable, which uses the
application executable in a second, headless process. See the
[library integration contract](../Watchdog/REFERENCE.md#automatic-application-startup)
for that setup.

The Host package deploys a sidecar next to your application. Its contract covers
the deployed payload and launch protocol.

Reference it directly from every executable that should carry a sidecar. Use
the public NekoLib.Watchdog API through automatic Sidecar startup or the
compatible manual WatchdogBootstrap.EnsureStarted call. The
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
