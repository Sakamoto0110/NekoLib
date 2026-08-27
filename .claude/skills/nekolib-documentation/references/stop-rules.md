# Stop Rules

**Document ID:** CLAUDE-DOCUMENTATION-STOP-RULES

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** mandatory stop conditions for module documentation work

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Stop, preserve evidence, and request disposition when any of these occurs:

- current source and a normative technical contract disagree;
- compiled API and an accepted API manifest disagree;
- the requested documentation outcome requires a product or public API change;
- the work would cross an active freeze or require an unfreeze;
- a finding lacks enough evidence to become a confirmed issue;
- a finding, issue, proposal, audit, external-evidence record, or owner decision
  would be promoted without formalization and acceptance;
- project, package, target, or boundary ownership cannot be established;
- the next action would leave the requested module or approved migration map;
- pre-existing dirty work overlaps the files being changed;
- preserving historical provenance would require rewriting the snapshot;
- validation evidence lacks the environment, boundary, target, or source
  baseline needed for the claim being made.

Do not use a stop as permission to broaden scope. Report the smallest exact
conflict and the authority sources that expose it.
