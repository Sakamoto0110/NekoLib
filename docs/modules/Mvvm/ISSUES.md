# NekoLib.Mvvm Confirmed Issues

**Document ID:** MVVM-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Mvvm boundary

**Surface:** issues

**Boundary:** mvvm

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

No confirmed Mvvm defect is recorded for the current source baseline, reviewed on
2026-08-29 at commit `418c6a4cab8f20c10fa8ff7df6589f74bd8af5ea`.

That conclusion is bounded by what was actually checked. The review read both
source files in full, both accepted API manifests, all 34 focused tests, the F1
audit and its two reconciliations, the migration guide, and the release records.
It reconciled every F1-MVVM disposition against current source and found each one
implemented as recorded. `TODO.md` carries no promoted Mvvm work and `ROADMAP.md`
declares no Mvvm freeze, so nothing is deferred or blocked here.

The defects the F1-MVVM review confirmed were all corrected before `1.0.0`: a
nullability contract that denied the null a binding genuinely supplies, and a
non-virtual notification method that gave a derived view-model no single place to
marshal to the UI thread. Both are preserved in [`CHANGELOG.md`](CHANGELOG.md),
[`HISTORY.md`](HISTORY.md), and the historical
[`audits/public-api-review-2026-08-17.md`](audits/public-api-review-2026-08-17.md).

Read this alongside the evidence limits rather than as broad assurance. No
WinForms or WPF binding pipeline has ever been driven by a test in this
repository, and concurrent `Execute` from multiple threads has never been
measured. Those gaps are carried in
[`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md) and
[`VALIDATIONS.md`](VALIDATIONS.md).

Unconfirmed observations belong in [`FINDINGS.md`](FINDINGS.md), and only
accepted scheduled work belongs in [`../../../TODO.md`](../../../TODO.md).
