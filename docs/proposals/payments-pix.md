# Payments and Pix Proposal

**Document ID:** PROPOSAL-PAYMENTS-PIX

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** unpromoted Payments/Pix module idea

**Surface:** proposal

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

## GLOBAL-PROPOSAL-001

**State:** exploring

**Idea:** Evaluate a narrow dual-target `NekoLib.Payments` package for immediate Pix charges, lookup, copy-and-paste payloads, and ambiguous-outcome reconciliation, using Efí homologation as the first provider model.

**Rationale:** A real payment integration is a plausible applied consumer of NekoLib.Http and deserves a domain-specific boundary instead of a universal payment abstraction.

**Constraints:** Consumer-owned persistence, HttpClient, credentials, certificates, OAuth, timeout, retry decisions, secrets, and authoritative PSP reconciliation; no production money movement in the first slice.

**Promotion target:** Owner decision on the package boundary, Efí sandbox, and first-slice scope; implementation is not promoted.

**Aliases:** G2

**Source:** [`payments-pix-design-review-2026-08-16.md`](../audit/payments-pix-design-review-2026-08-16.md)

The design investigation is complete, but the implementation decision remains
open. Promotion may use the linked review directly; this proposal is only the
concise idea record.
