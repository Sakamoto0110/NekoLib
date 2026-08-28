# NekoLib.Navigation Findings

**Document ID:** NAV-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about the NekoLib.Navigation family

**Surface:** findings

**Boundary:** navigation

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## NAV-FINDING-001

**Status:** confirmed

**Confidence:** high

**Observation:** Historical alias `NAV-EXT-001`: `GuardAttribute.RedirectTo` is public, but the wrapper that applies it to built-in attributes is internal. A custom guard attribute must implement redirect behavior in its returned `IGuard`; merely assigning `RedirectTo` does not change its result.

**Evidence:** `src/Navigation/NekoLib.Navigation/Metadata/Attributes/GuardAttribute.cs`, the accepted core API baselines, `GuardAttributeContractTests`, and [`../../audit/public-api-documentation-extensibility-review-2026-08-27.md`](../../audit/public-api-documentation-extensibility-review-2026-08-27.md)

**Hypothesis:** A consumer may infer behavioral parity with built-in guard attributes from the public property and accidentally ship a deny-only custom attribute.

**Disposition:** Keep the implemented asymmetry explicit in [`REFERENCE.md`](REFERENCE.md). Any helper or behavior change requires a separate public-API decision and a narrow unfreeze; none is scheduled.

**Outcome link:** [`REFERENCE.md#writing-a-custom-guard-attribute`](REFERENCE.md#writing-a-custom-guard-attribute)

## NAV-FINDING-002

**Status:** open

**Confidence:** low

**Observation:** During the clean 20-minute WPF `net9.0-windows` E3-NAV smoke, private bytes moved from 152.6 MiB at sustained-phase entry to 170.8 MiB while handles oscillated, managed heap was non-monotonic, and scenario-owned state returned to zero at cleanup.

**Evidence:** [`../../../runtime_tests/Navigation/LongRunningRecovery/README.md`](../../../runtime_tests/Navigation/LongRunningRecovery/README.md), qualifying commit `897e17b5e9000af4e90a1cc3783e271216cc9b9f`

**Hypothesis:** The movement may be native/WPF warm-up, allocator retention, measurement noise, or slow resource growth; the single short window cannot distinguish these explanations.

**Disposition:** Do not classify a leak. A targeted longer WPF run with repeated samples and the same clean cleanup assertions is recommended only when resource-risk evidence is requested or a new adapter-specific symptom appears.

**Outcome link:** none

## NAV-FINDING-003

**Status:** confirmed

**Confidence:** high

**Observation:** Visual Studio's WinForms designer cannot open a consumer surface whose direct base remains generic. A prompt based on `PromptViewBase<TResult>` therefore needs a consumer-owned, non-generic closed shim such as `ReasonPromptBase : PromptViewBase<string>`.

**Evidence:** [`audits/design-time-2026-08-06.md`](audits/design-time-2026-08-06.md) and `runtime_tests/Data/FarmDatabase`

**Hypothesis:** Moving the result type off the view could remove the shim, but that would change the stable prompt public contract and introduce runtime casting.

**Disposition:** Preserve the current typed contract and document the shim. No public-API change is accepted or scheduled.

**Outcome link:** [`REFERENCE.md#design-time-authoring`](REFERENCE.md#design-time-authoring)
