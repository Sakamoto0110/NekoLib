# Module Documentation Self-Review

**Document ID:** CLAUDE-DOCUMENTATION-SELF-REVIEW

**Schema version:** 1

**Kind:** guide

**Lifecycle:** current

**Subject:** completion checklist for one module documentation boundary

**Surface:** guide

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Before declaring a module complete, verify all of the following:

- every required surface and directory exists;
- metadata and stable IDs satisfy the schema;
- every schema-backed metadata value occupies one physical line;
- local links and anchors resolve with repository casing;
- one and only one normative technical reference owns the boundary;
- source-adjacent or compatibility portals declare the combined
  `Surface: portal`, `Authority role: portal`, `Indexing: pointer-only`, and
  `Canonical` metadata contract, then contain only routing metadata and one
  canonical link;
- historical documents remain historical, retain provenance, and declare
  `Mutation: snapshot` when their surface is an audit;
- findings are not presented as facts;
- backlog is not presented as roadmap;
- validation evidence is not presented as contract;
- compiled public API is referenced, not manually duplicated;
- current technical claims match source and project files;
- migration guidance remains historically faithful;
- confirmed issues cite evidence;
- validation requirements follow architecture and risk;
- evidence gaps, exclusions, and supersession are explicit;
- requirement classification and evidence status remain separate;
- the manifest is the only surface that declares the inherited profile list;
- inherited validation profiles were not weakened silently;
- soak evidence records workload, recovery, resources, acceptance, and cleanup;
- `HISTORY.md` remains chronological and append-only;
- audit original paths and baselines remain discoverable;
- every changed document is interpretable from repository metadata, authority,
  links, and evidence without Claude-specific context;
- the Claude test and evidence emphasis did not narrow a general documentation
  surface or introduce private vocabulary;
- no product/API change, roadmap promotion, freeze change, or adjacent module
  migration was absorbed without authorization.

Run the documentation verifier and diff hygiene checks, then report source,
build, test, API, runtime, interactive, package, and release evidence as separate
layers. If a skill adapter, shared skill contract, or the skill registry changed,
also run `eng/verify-skills.ps1`. State explicitly which layers were not run.
