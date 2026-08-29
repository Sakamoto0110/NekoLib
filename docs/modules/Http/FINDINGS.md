# NekoLib.Http Findings

**Document ID:** HTTP-FINDINGS

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** unconfirmed and non-normative observations about the NekoLib.Http boundary

**Surface:** findings

**Boundary:** http

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

Everything here is non-normative. A finding becomes an issue only after it is
verified, and scheduled work only after explicit promotion to
[`TODO.md`](../../../TODO.md).

## HTTP-FINDING-001

**Status:** open

**Confidence:** medium

**Observation:** `HttpApiCatalog.Endpoints` is documented as returning endpoints "in registration order", but that order comes from enumerating a `Dictionary<string, HttpEndpoint>`. Dictionary enumeration order is explicitly not part of the BCL contract, and no test asserts the documented order.

**Evidence:** The builder accumulates into a `Dictionary` with `StringComparer.OrdinalIgnoreCase`; `Build()` copies it into another `Dictionary`; the catalog constructor materializes `Endpoints` from `byName.Values`. The two catalog tests cover duplicate-name rejection and post-build registration only. Nothing pins ordering.

**Hypothesis:** For an add-only dictionary both current runtimes enumerate in insertion order, because entries are appended to a backing array and nothing is removed, so the documented behavior almost certainly holds today on `net481` and `net9.0`. The risk is that a guarantee is being published on the strength of an implementation detail: a future runtime change, or a later feature that removes or replaces a registration, could break it silently and no test would notice.

**Disposition:** Keep as a finding. Changing `Endpoints` to a list-backed order would be an implementation change outside a documentation review, and weakening the documented guarantee would be a consumer-visible contract reduction — neither is authorized here. The reference now records that the order is not enforced by a test. Adding a pinning regression is the cheapest resolution if this is ever promoted.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## HTTP-FINDING-002

**Status:** open

**Confidence:** high

**Observation:** `JsonHttpBodySerializer(JsonSerializerSettings)` stores the caller's settings object by reference and uses it for every serialization and deserialization call. A caller that mutates that instance after constructing the serializer changes the behavior of a live client, and Newtonsoft settings are not designed to be mutated while a serialization is in flight.

**Evidence:** The constructor assigns `_settings = settings ?? throw ...` with no copy, and both `Serialize` and `Deserialize` pass `_settings` straight to `JsonConvert`. The parameterless constructor allocates a fresh `JsonSerializerSettings` and is therefore unaffected. No test covers post-construction mutation.

**Hypothesis:** The usual composition — build settings, construct the serializer, never touch the settings again — is unaffected, which is why this has never surfaced. An application that reuses one settings instance across several serializers and adjusts it later would see the change reach an already-constructed client, and under concurrent sends could observe a torn read of Newtonsoft's internal state.

**Disposition:** Keep as a finding, not a defect. Copying the settings would change documented behavior — the XML comment states that the instance is retained — and `JsonSerializerSettings` has no cheap deep clone. The reference now states the consequence so consumers can decide to hand each serializer its own instance. No change is scheduled.

**Outcome link:** [`REFERENCE.md`](REFERENCE.md)

## HTTP-FINDING-003

**Status:** open

**Confidence:** high

**Observation:** No recorded run has ever exercised this module over a real socket except two bounded provider runs against one provider on one date. Every deterministic test, and every probe the F1 review performed, used an in-process `HttpMessageHandler`.

**Evidence:** The deterministic suite injects a controlled handler; the F1 audit's residual limits state in both its snapshot and its reconciliation that "all probes used an in-process `HttpMessageHandler`, so no real socket, TLS, proxy, redirect, compression, or HTTP/2 behavior was exercised". The only real-transport evidence is the TheCatAPI scenario's `net481` and `net9.0` runs on 2026-08-16.

**Hypothesis:** The module delegates transport entirely to a consumer-owned `HttpClient`, so most real-transport behavior is genuinely not its contract — which is the reason the gap has been acceptable. Two areas are less clearly outside it: response reading interacts with `HttpCompletionOption.ResponseHeadersRead` and streamed decompression, and the documented timeout shape is a `HttpClient` behavior this module reports rather than owns. Neither has been observed against a real server.

**Disposition:** Keep as a finding and carry the qualification gap explicitly as [`HTTP-VALREQ-012`](VALIDATION_REQUIREMENTS.md). Do not read the deterministic suite as transport evidence, and do not read the 2026-08-16 provider runs as anything beyond the flow and date they record. No change is scheduled.

**Outcome link:** [`VALIDATION_REQUIREMENTS.md`](VALIDATION_REQUIREMENTS.md)
