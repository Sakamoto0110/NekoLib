# Authority Traversal

**Document ID:** CLAUDE-DOCUMENTATION-AUTHORITY-TRAVERSAL

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** source precedence and conflict handling for module documentation campaigns

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Use each source only for the facts it owns:

1. Current source and project files establish implementation, targets,
   dependencies, build, and package behavior.
2. Compiled assemblies establish actual public API; accepted manifests under
   `eng/public-api/` establish the reviewed stable baseline.
3. Current module `REFERENCE.md` and global policies establish the intended
   documented contract.
4. Root `TODO.md` establishes accepted work, order, freezes, and gates;
   confirmed issues establish defect status but not scheduling.
5. Tests, runtime scenarios, package probes, and evidence records demonstrate
   bounded claims. They do not define behavior.
6. Migrations and changelogs describe consumer transition and evolution.
7. History and audits preserve chronology and rationale at their recorded
   baselines.
8. Findings, backlog, and historical agent guidance are non-normative leads.

Use full-source inspection to correct stale historical assumptions in the new
current model, but do not rewrite historical snapshots. If current source and a
normative reference conflict, record the exact conflict and stop for disposition
instead of silently choosing prose. If an accepted API baseline conflicts with
the assembly, stop as an API mismatch.
