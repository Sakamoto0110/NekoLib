# Unofficial TheCatAPI Provider Probe

**Kind:** guide

**Lifecycle:** current

**Owner:** NekoLib.Http

**OS / target:** Windows; `net481` or `net9.0`

**Prerequisites:** internet access, a maintainer-owned TheCatAPI key in
`NEKOLIB_THECATAPI_KEY`, and a writable repository `artifacts/` directory

**Last verification:** 2026-08-16, implementation commit `ae711fb`; Debug build
passed on `net481` and `net9.0` with 0 warnings and 0 errors. Missing-key and
real-provider paths executed on both targets; both provider runs passed 10/10,
exited `0`, and reconciled cleanup

## Purpose

This optional standalone scenario is the first external provider model for
`NekoLib.Http`. It proves that one typed catalog can express GET, POST and
DELETE operations while the consumer continues to own the base address,
credential header, timeout and `HttpClient` lifetime.

The scenario searches for one image, reads it by id, creates a favourite with a
random run-owned `sub_id`, queries that favourite, deletes it and confirms its
absence. A `finally` cleanup queries the same `sub_id` and removes every
run-owned favourite even when an earlier check fails.

This is an unofficial interoperability probe. It is not affiliated with or
endorsed by TheCatAPI. The provider's current public entry point and terms are:

- <https://thecatapi.com/>
- <https://thecatapi.com/terms>
- <https://thecatapi.com/privacy>

## Build

Build both supported targets explicitly from the repository root:

```powershell
dotnet build runtime_tests\Http\TheCatApi\NekoLib.Http.RuntimeTests.TheCatApi.csproj -f net481
dotnet build runtime_tests\Http\TheCatApi\NekoLib.Http.RuntimeTests.TheCatApi.csproj -f net9.0
```

Build success proves source compatibility only. It does not prove provider
connectivity or mutation/cleanup behavior.

## Launch

Use a key owned for this probe. Do not paste it into a command transcript,
source file or artifact:

```powershell
$env:NEKOLIB_THECATAPI_KEY = Read-Host "TheCatAPI key"
dotnet run --project runtime_tests\Http\TheCatApi\NekoLib.Http.RuntimeTests.TheCatApi.csproj -f net9.0 -c Release
Remove-Item Env:NEKOLIB_THECATAPI_KEY
```

Repeat with `-f net481` only when provider parity on the legacy target is
needed. A normal run is bounded to two minutes and each HTTP attempt is bounded
to 15 seconds. The scenario performs no automatic retry of POST or DELETE.

## Procedure and expected result

The executable determines its own verdict:

1. Missing `NEKOLIB_THECATAPI_KEY` exits `3` without sending a request.
2. Search and lookup must return coherent typed image data.
3. Favourite creation must return a positive id.
4. Listing by the unique run `sub_id` must find the created favourite.
5. Deletion must succeed and the favourite must disappear within the bounded
   observation window.
6. Cleanup must find no remaining favourite owned by the run.

Exit `0` means every check passed and cleanup reconciled. Exit `3` is a missing
credential or unreachable provider, `4` is a wrong provider/contract outcome,
`5` is a timeout, `6` is incomplete cleanup, `7` is an unexpected scenario
failure, and `8` is an interrupted run.

## Artifacts, cleanup and side effects

Each invocation creates:

```text
artifacts/validation/http/thecatapi-<tfm>-<timestamp>/result.json
```

`result.json` contains endpoint/check names, status-derived outcomes, timing,
the random non-personal `sub_id`, exit code and cleanup problems. It never
contains the API key, request/response bodies or authentication headers.

The only provider-side mutation is a favourite scoped by the generated
`nekolib-http-<guid>` value. Cleanup queries and deletes every favourite with
that exact value. If exit `6` is reported, use the recorded `sub_id` with the
same account to locate and remove the residue before discarding the artifact.

## Verification record

On 2026-08-16 the missing-key path ran independently on `net481` and `net9.0`.
Both invocations exited `3`, recorded `ApiKeyPresent: false`, sent no provider
request, reported zero cleanup problems, and produced sanitized artifacts under
`artifacts/validation/http/`. This proves prerequisite handling, artifact
finalization and the no-request boundary; it is not provider evidence.

Later on 2026-08-16, real-provider runs passed on both targets:

| Target | Exit / checks | Provider outcomes | Cleanup | Artifact |
|---|---|---|---|---|
| `net9.0` | `0`; 10/10 passed | Search and lookup returned HTTP 200; favourite creation returned HTTP 201; query and deletion passed | Zero cleanup problems; explicit deletion and no run-owned favourite remaining | `artifacts/validation/http/thecatapi-net9.0-20260816T222349134Z/result.json` |
| `net481` | `0`; 10/10 passed | Search and lookup returned HTTP 200; favourite creation returned HTTP 201; query and deletion passed | Zero cleanup problems; explicit deletion and no run-owned favourite remaining | `artifacts/validation/http/thecatapi-net481-20260816T222415829Z/result.json` |

Both artifacts were inspected after execution. They record that a key was
present but contain no key value, credential header, request body, response
body, or personal `sub_id`. This is provider interoperability evidence for the
declared flow on the execution date; it does not establish provider uptime or
policy beyond these bounded runs.
