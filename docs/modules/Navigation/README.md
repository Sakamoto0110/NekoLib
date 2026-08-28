# NekoLib.Navigation

**Document ID:** NAV-INTRODUCTION

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** concise consumer introduction to the NekoLib.Navigation family

**Surface:** introduction

**Boundary:** navigation

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

NekoLib.Navigation is the deterministic page-lifecycle runtime for unattended,
touch-first, single-window WinForms and WPF applications. The core package owns
registration, navigation order, history, guards, session and idle behavior,
overlays, and optional diagnostics. The WinForms and WPF packages supply native
hosts, view bases, interaction adapters, and positioning helpers.

Applications normally reference `NekoLib.Navigation.WinForms` or
`NekoLib.Navigation.Wpf`, compose a native host through
`PageNavBootstrap.Use<TPlatform>(host)`, and navigate through the intentional
process-wide `NavigationService` facade. Always await
`NavigationService.Shutdown()` before mounting a fresh context.

Navigation owns view lifetime and navigation coordination; it is not a general
UI framework, dependency-injection container, authentication provider, or
multi-window router. Start with the [technical reference](REFERENCE.md) for the
complete lifecycle and extension contract. Use the
[module manifest](MANIFEST.md) for packages, targets, API baselines, evidence,
audits, and migration routes.
