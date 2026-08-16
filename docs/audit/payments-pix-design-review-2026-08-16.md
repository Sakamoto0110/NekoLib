# Payments and Pix Design Review — 2026-08-16

**Kind:** audit

**Lifecycle:** current

**Subject:** code-first Phase G2 review of the payment-module boundary, Pix
immediate-charge scope, HTTP ownership, provider selection, security,
reconciliation, and executable evidence

**Status:** review complete; implementation decision pending

**Reference date:** 2026-08-16

**Reference commit:** `f73ba4a2fd01b66f5df6c172ba15d6d39d01a072`

**Last reconciliation:** none

**Current state:** [`TODO.md`](../../TODO.md) Phase G2 is the sole authority for
promotion and implementation status

## Baseline and authority

This review covers the committed source at the reference commit on branch
`phase-e/sqlserver-and-orchestration`. The worktree and index were clean, the
branch was 65 commits ahead of `origin/master`, and nothing had been pushed.

The review changed no product code. Its only repository changes are this audit,
the audit indexes, and a roadmap status that records the completed review while
keeping implementation gated. The source, project files, executable tests,
official API Pix specification, and provider documentation are the evidence;
the recommendations below are not accepted implementation work until the user
approves them explicitly.

## Scope

Included:

- the current `NekoLib.Http` public surface, tests, package topology, and
  ownership rules;
- current source/project searches for an existing payment, money, charge, Pix,
  or provider abstraction;
- the official API Pix immediate-charge contract;
- current official Efí and Mercado Pago sandbox documentation;
- a bounded merchant-receiving slice suitable for unattended PDV/DM clients;
- credential, mTLS, OAuth2, persistence, idempotency, reconciliation, artifact,
  and dual-target constraints.

Excluded:

- product projects, public types, tests, runtime scenarios, solution entries,
  package entries, or provider credentials;
- production or real-money calls;
- legal, accounting, tax, PCI, KYC, fraud, chargeback, or commercial-provider
  suitability advice;
- webhooks, refunds, due-date charges, Pix Automatic, outgoing Pix, split,
  recurring billing, cards, boleto, or a generic checkout/order framework;
- changes to `NekoLib.Http`, Core, Data, Navigation, or any frozen module.

## Observed repository state

No payment-domain implementation exists under tracked `src/`, `tests/`, or
`runtime_tests/`. The only payment-related transport example is the generic
idempotency-header assertion in the HTTP tests; it is not a payment contract.

`NekoLib.Http` already supplies the required transport mechanics:

- immutable, instance-scoped typed endpoint catalogs;
- GET, POST, PUT, PATCH, DELETE and custom methods;
- safe relative route/query construction;
- consumer-owned `HttpClient`, base address, authentication, certificates,
  timeouts, handlers and retry policy;
- cancellation, bounded response buffering, typed successful bodies and raw
  non-success protocol evidence;
- deterministic `net481`/`net9.0` tests and package-backed consumers.

Evidence:

- [`NekoLib.Http.csproj`](../../src/Http/NekoLib.Http/NekoLib.Http.csproj)
- [`HttpEndpoint.cs`](../../src/Http/NekoLib.Http/HttpEndpoint.cs)
- [`HttpApiClient.cs`](../../src/Http/NekoLib.Http/HttpApiClient.cs)
- [`HttpApiResponse.cs`](../../src/Http/NekoLib.Http/HttpApiResponse.cs)
- [HTTP technical reference](../../src/Http/NekoLib.Http/README.md)
- [`HttpApiClientTests.cs`](../../tests/NekoLib.Http.Tests/Unit/HttpApiClientTests.cs)

The HTTP layer deliberately does not interpret Pix, OAuth, webhooks,
idempotency, provider errors, or reconciliation. That is the correct boundary.
Changing it would weaken both modules and is not required by the reviewed
flow.

## Pix is a rail and standard, not the provider

The Banco Central API Pix specification standardizes services supplied by a
receiving PSP: charge management, received-Pix tracking, refunds and queries.
The PSP still owns the merchant relationship, actual host, granted products,
authentication and operational policy. The BCB repository is a specification,
not a merchant sandbox or account provider.

The reviewed official OpenAPI release is 2.9.0. It explicitly treats added
optional fields and new enum elements as backward-compatible evolution.
Therefore a NekoLib client must ignore unknown response fields and preserve an
unknown raw status instead of deserializing into a closed enum that can break
when the standard grows.

For immediate charges, the official contract supports caller-selected
`PUT /cob/{txid}`, provider-selected `POST /cob`, and lookup through
`GET /cob/{txid}`. A caller-selected `txid` is the safer first PDV contract:
the consumer can persist it before the request and reconcile an ambiguous
timeout or transport loss by lookup instead of issuing a second charge.

## First provider model

| Candidate | Evidence | Review disposition |
|---|---|---|
| API Pix specification / Banco Central | Authoritative functional contract and versioning model; no merchant account or executable sandbox | Use as the semantic authority, not as a provider. |
| Efí Bank homologation | Dedicated homologation host, separate credentials/certificate, OAuth2, mTLS, immediate-charge endpoints, QR/copy payload, lookup, and deterministic simulation of active/completed charges | **Recommended first external model.** It exercises the real security and reconciliation boundaries while remaining close to API Pix. |
| Mercado Pago test environment | Test credentials and a Pix test flow exist, but the public model reviewed here is centered on `/v1/orders` and broader checkout concepts | Defer as a valuable second provider. Using it first would bias the framework toward one provider's order/checkout aggregate. |

Efí is not treated as universally reliable or permanently free. Current
official material advertises a sandbox and accounts without opening or monthly
maintenance fees; transaction and service terms can change and must be
rechecked before every external validation. A test still requires an approved
account, homologation `Client_Id`, `Client_Secret`, and a provider-issued
P12/PEM certificate.

## Recommended product boundary

Create one package only: `NekoLib.Payments`, targeting `net481;net9.0` and
referencing `NekoLib.Http`. Do not add a Core reference. Keep the first public
surface explicitly Pix-oriented under `NekoLib.Payments.Pix`; do not invent a
universal `IPaymentProvider`, checkout, order, customer, cart, refund, webhook,
or multi-rail state model before a second real consumer proves a common shape.

One package is preferable to speculative `Contracts`, `Pix`, and `Efí`
assemblies. The Pix implementation is the first real consumer of its own public
types, and the Efí scenario is the external oracle. A provider-specific product
assembly becomes justified only when a genuine deviation cannot remain in
consumer composition or the runtime scenario.

### First public behavior

The first slice should expose behavior equivalent to:

1. validate a positive BRL amount without binary floating-point conversion;
2. validate a caller-selected alphanumeric `txid` of 26–35 characters;
3. create one immediate charge through `PUT /v2/cob/{txid}`;
4. return the `txid`, expiration, copy-and-paste payload, known status category,
   raw provider status, and bounded protocol/provider evidence;
5. look up the authoritative charge by `txid`;
6. reconcile an ambiguous create outcome by lookup; and
7. tolerate additive JSON fields and unknown status values.

The library should return the Pix copy-and-paste/BR Code string. Rendering a
bitmap/SVG QR code belongs to the consumer UI or a separately justified
adapter. Efí's `imagemQrcode` and visualization link are provider conveniences,
not framework contracts.

### Ownership

The consumer must own:

- the merchant account, Pix key and commercial provider choice;
- durable persistence of `txid` and application order/reference before network
  execution;
- the configured `HttpClient`, base address and handler lifetime;
- OAuth token acquisition/renewal and mTLS certificate loading/lifetime;
- secrets, certificate files, proxy, timeout and retry decisions;
- correlation between the application sale and the provider charge;
- polling cadence and authoritative reconciliation after restart, timeout, or
  uncertain transport outcome;
- display, printing, QR rendering, receipt and user interaction.

The package owns validation, endpoint construction, safe serialization,
tolerant response mapping, exactly one send per requested operation, and an
explicit lookup path. It must never automatically retry charge creation, hide
an ambiguous outcome, treat local state as proof of payment, store credentials,
log bodies/headers, or run a background poller.

## Security and correctness constraints

- Use `decimal`-based input and invariant two-decimal serialization; never use
  `double` for money.
- Persist the merchant correlation and `txid` before sending the PUT. The
  provider lookup is the recovery mechanism after a lost response.
- Treat payment as complete only when the provider's authoritative response
  establishes completion. HTTP 2xx for charge creation does not mean paid.
- Preserve unknown provider status text and map known states without a closed
  enum failure mode.
- Exclude debtor CPF/CNPJ/name from the first slice. They are optional for the
  reviewed flow and would introduce unnecessary personal data.
- Never print or persist `Client_Secret`, access tokens, certificate private
  keys, request/response bodies, or authorization headers in tests/artifacts.
- Keep the P12/PEM outside the repository and generated artifact tree. Record a
  one-way certificate fingerprint only if provenance later requires it.
- Do not add automatic retries. Even when PUT is structurally idempotent, the
  application must decide when to reconcile and when a retry is safe.
- Do not accept production credentials or real-money execution in the first
  runtime scenario.

## Verification design if accepted

### Deterministic dual-target contracts

Use a controlled `HttpMessageHandler` and cover:

- amount formatting/validation and `txid` validation;
- exact PUT/GET routes and request JSON;
- consumer-owned auth/certificate boundary;
- active and completed charge mapping;
- unknown fields and unknown status tolerance;
- protocol errors, provider error envelopes, malformed success bodies, size
  bounds and cancellation;
- one mutating send per call and no automatic retry;
- a simulated lost create response followed by successful lookup/recovery;
- no credential/body content in exception messages or test artifacts.

### Efí homologation scenario

Build both targets first. The executable scenario should accept credentials and
certificate path only through process environment, persist a sanitized plan
before its first request, and decide by exit code. Run `net9.0` first, inspect
the artifact and provider state, then repeat `net481` only after a clean pass.

The scenario should use synthetic, non-personal identifiers and prove at least
one active charge and one simulated completed charge using the documented
homologation amounts. Payment-provider records are audit history and may not be
deletable; cleanup must mean no active run-owned work requiring action, not a
false claim that the provider erased completed records. The artifact must state
the expected retained sandbox records.

Webhooks remain excluded. They require a separately reviewed public callback,
origin/authentication policy, replay handling, durable inbox, duplicate-event
semantics and shutdown lifecycle. Polling by `txid` is sufficient for the first
proof.

## Rejected first-slice alternatives

| Alternative | Reason rejected for the first slice |
|---|---|
| Put Pix/OAuth/certificate helpers in `NekoLib.Http` | Violates the established provider-neutral HTTP boundary. |
| Start with a generic `IPaymentProvider` and universal status enum | No second rail/provider proves those abstractions; a closed enum is incompatible with additive API Pix evolution. |
| Create separate Payments.Contracts, Payments.Pix and Payments.Efí projects | Three assemblies before one implemented consumer add topology without evidence. |
| Start with Mercado Pago Orders | Useful future comparison, but its checkout/order aggregate would dominate the initial model instead of the API Pix charge contract. |
| Include webhook, refund and cancellation immediately | Each adds distinct security, persistence and semantic boundaries not required to create and reconcile a charge. |
| Generate the client from OpenAPI | Adds generated surface, regeneration/version policy and provider drift before the small hand-written contract proves value. |
| Validate with production credentials or real money | Unnecessary and unsafe; homologation can exercise the declared first slice. |

## Decision required

Implementation remains gated. The user must explicitly accept or revise all
three of these choices:

1. one `NekoLib.Payments` package with a Pix-specific first public surface and
   a direct dependency on `NekoLib.Http`;
2. Efí Bank homologation as the first executable provider model; and
3. v1 limited to caller-persisted immediate-charge PUT, lookup, copy-and-paste
   payload, tolerant status/evidence and reconciliation, with the listed
   exclusions.

After acceptance, promote concrete implementation checkboxes to `TODO.md`, then
implement in a fresh package version. Do not infer approval from this audit.

## External references

Accessed 2026-08-16:

- Banco Central do Brasil, API Pix OpenAPI 2.9.0:
  <https://github.com/bacen/pix-api/blob/master/openapi.yaml>
- Banco Central do Brasil, Manual de Padrões para Iniciação do Pix:
  <https://www.bcb.gov.br/content/estabilidadefinanceira/pix/Regulamento_Pix/II_ManualdePadroesparaIniciacaodoPix.pdf>
- Efí, credentials, certificate, authorization and environment hosts:
  <https://dev.efipay.com.br/docs/api-pix/credenciais/>
- Efí, immediate charges and homologation behavior:
  <https://dev.efipay.com.br/docs/api-pix/cobrancas-imediatas/>
- Efí, payload/QR locations:
  <https://dev.efipay.com.br/docs/api-pix/payload-locations/>
- Efí, API Pix sandbox overview:
  <https://sejaefi.com.br/efi-pay/api-pix>
- Efí, current digital-account access and fee overview:
  <https://sejaefi.com.br/efi-bank>
- Mercado Pago, Pix test flow for Orders:
  <https://www.mercadopago.com.br/developers/pt/docs/checkout-api-orders/integration-test/pix>
