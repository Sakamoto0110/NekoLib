# Module Manifest Template

**Document ID:** TEMPLATE-MANIFEST

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** template for a module documentation manifest

**Surface:** template

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** exclude

Copy this file to `docs/modules/<Boundary>/MANIFEST.md`, replace every template
value, and keep routing facts aligned with project and package sources.

Required manifest metadata:

```text
Projects: repository-relative csproj paths, comma-separated
Packages: package IDs, comma-separated, or none
Targets: target frameworks, comma-separated
Project dependencies: referenced project package IDs, comma-separated, or none
Package dependencies: PackageReference IDs, comma-separated, or none
Solution membership: included or excluded
Distribution: shipped-library, deployment-package, or unshipped
Stability: stable, preview, experimental, or unshipped
Experimental APIs: stable experimental IDs, comma-separated, or none
API baselines: repository-relative accepted manifest paths, comma-separated, or none
Profiles: validation profile IDs, comma-separated
Technical reference: repository-relative REFERENCE.md path
Related boundaries: boundary keys, comma-separated, or none
Source: repository-relative source paths, comma-separated
Tests: repository-relative test paths, comma-separated, or none
Runtime scenarios: repository-relative scenario paths, comma-separated, or none
Package evidence: repository-relative release or package evidence paths, comma-separated, or none
```

The body must route the consumer introduction, technical reference, optional
internals, history, changelog, issues, findings, relevant global proposals,
validation requirements, validation evidence, audits, migrations, source, tests,
runtime scenarios, release evidence, and related boundaries. Do not reproduce
the public symbol inventory.
