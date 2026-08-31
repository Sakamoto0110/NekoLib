# NekoLib.Navigation Changelog

**Document ID:** NAV-CHANGELOG

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** consumer-visible evolution of the NekoLib.Navigation family

**Surface:** changelog

**Boundary:** navigation

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The [coordinated family changelog](../../../CHANGELOG.md) remains the release
summary. This file records Navigation-specific consumer impact without
duplicating package hashes or release provenance.

## 1.1.0

**Packages:** `NekoLib.Navigation`, `NekoLib.Navigation.WinForms`, `NekoLib.Navigation.Wpf`

**Compatibility class:** additive

**Consumer impact:** Package candidates produced through the corrected flow deliver XML member documentation for the accepted public and protected API; compiled signatures and runtime behavior are unchanged.

**Migration:** none

- Documentation-enabled builds produce XML assets for all six target
  assemblies. Immutable candidate `1.1.0-local.9` is the qualifying package
  evidence that each package contains its package-owned XML file and that
  isolated PackageReference consumers receive it.

## 1.0.0

**Packages:** `NekoLib.Navigation`, `NekoLib.Navigation.WinForms`, `NekoLib.Navigation.Wpf`

**Compatibility class:** mixed

**Consumer impact:** The pre-stable candidate surface was corrected before the first stable contract; consumers upgrading from an earlier candidate may require source changes and recompilation.

**Migration:** `docs/modules/Navigation/migrations/f1.md`

- `NavigationService.SwitchPage` accepts `NavigationArgs?` and returns a
  call-scoped `NavigationResult` that distinguishes success, denial, and final
  redirect destination.
- Registration composition, descriptor copying, guard redirects,
  `AllowAnonymous`, session/history ownership, lifecycle and teardown admission,
  and consumer extension boundaries were finalized.
- WinForms and WPF adapter nullability, ownership, event naming, dispatch,
  disposal, and toolkit contracts were aligned with their implemented native
  behavior while preserving intentional platform differences.
