# NekoLib 1.0.0 Stable Family Baseline

**Kind:** reference

**Lifecycle:** current

**Subject:** first stable coordinated family release and support baseline

**Declaration date:** 2026-08-21

**Qualifying source commit:**
`7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Qualifying package candidate:** `1.0.0-local.22`

## Declaration

NekoLib `1.0.0` is the first stable coordinated family support baseline. The
30 accepted compiled public API manifests and the accepted Watchdog Host
deployment contract at the qualifying source commit are the initial stable
baselines under the
[`public API and release policy`](public-api-release-policy.md).

All accepted unmarked public and protected surfaces are stable from `1.0.0`.
`IInspectionRecorder.RegisterAction` remains explicitly experimental under
`NEKOEXP0001`; this declaration does not promote it.

The declaration records a support and compatibility boundary. It does not
claim remote distribution: no `1.0.0` package was pushed to a remote feed, no
Git tag was created, and no branch was pushed.

## Qualifying package evidence

The canonical command was run from the clean qualifying source commit without
`-SkipTests`:

```powershell
.\eng\pack-local.ps1 -PackageVersion 1.0.0-local.22
```

| Gate | Result |
|---|---|
| Source provenance | Clean commit `7090e40eed7c6b888ce8da732f21cbe10f1a936c`; every `.nupkg` records the same repository commit |
| Solution tests | 1,670 passed, 0 failed, 0 skipped |
| Coordinated packages | 16 `.nupkg` and 15 `.snupkg` artifacts retained |
| Watchdog Host payloads | `net481`, `net9.0-windows7.0/win-x86`, and `net9.0-windows7.0/win-x64` validated |
| External consumers | PackageReference-only WinForms and WPF consumers passed on `net481` and `net9.0-windows`; consumer builds emitted 0 warnings and 0 errors |
| Host negative probe | Unsupported `win-arm64` deployment failed as required |
| Host runtime probes | Packaged protocol mismatch, startup, and stop probes passed on both target families |
| Public API | All 30 compiled manifests passed `eng\verify-public-api.ps1 -NoBuild` |
| Release rebuild | 202 baseline warning occurrences, 0 errors, and no new normalized warning identity |
| Documentation | `eng\verify-docs.ps1` passed against the captured Release rebuild log |

The deterministic aggregate SHA-256 over the sorted UTF-8 sequence
`filename=SHA256\n` for all 31 retained package artifacts is:

```text
15EB21F30EB7D3C4BDDC39F39EE32A6D17536D2973E5C2C36416686234CC425A
```

## Main package hashes

| Package | SHA-256 |
|---|---|
| `NekoLib.Core.1.0.0-local.22.nupkg` | `6C7E3C6B7D40F5690B2600739C5703BCC7C73006E659BDD7705B7344A6045946` |
| `NekoLib.Data.1.0.0-local.22.nupkg` | `56E66DAC1026C97E610929C0B07EAC8B6668DBE32DCDAA1E31184352A4BD4DCF` |
| `NekoLib.Devices.1.0.0-local.22.nupkg` | `EA3008BACDE1A3F7EF913F0077E6C653C79B215D60D6896E9BAEE6C428429F98` |
| `NekoLib.Diagnostics.1.0.0-local.22.nupkg` | `CAEB7E379AF08E2EDFA162F6AD387C3365B66F9BE13BE55E14B1C284FE8BF73D` |
| `NekoLib.Diagnostics.Windows.1.0.0-local.22.nupkg` | `F982CFFF68645E7345C3798FABE77D33C8836CDB912B597FFDA7619770717181` |
| `NekoLib.Http.1.0.0-local.22.nupkg` | `2B505F4B426FE0C719256C71AE4FA6F59D8877F99E9447275AD21A9C6A4C208E` |
| `NekoLib.Inspection.1.0.0-local.22.nupkg` | `5D6D8667FCC292DC2F505FEB55BE15B5457204ECD069837EB8436348A651194C` |
| `NekoLib.Logging.1.0.0-local.22.nupkg` | `463BC9708E7E6CDEDCE74EBCF339699545DF023B5058E9E41A21B483E0F20C23` |
| `NekoLib.Mvvm.1.0.0-local.22.nupkg` | `BBDD741CD2CF25892DA5A0F677E2E37AA5984317121D110BC05CA7F9E3F17D2D` |
| `NekoLib.Navigation.1.0.0-local.22.nupkg` | `6F450B5938F3AC55FC40DB80144F587749D86BDA5908721BEF945778CFB6E790` |
| `NekoLib.Navigation.WinForms.1.0.0-local.22.nupkg` | `B269E17D3BC50719ED019F2E6D92AF5F92D791642FEFA1B5A67F338856843DE6` |
| `NekoLib.Navigation.Wpf.1.0.0-local.22.nupkg` | `EA3148F0A0B26BAE65BD8C8D9592E5AACE06B6624587F7F2CE19ED6972633D96` |
| `NekoLib.Pipes.1.0.0-local.22.nupkg` | `F03216E18D48833C1260A97259F96771ACF75C67B6AA0DEE5F0871AEB3921FDB` |
| `NekoLib.Telemetry.1.0.0-local.22.nupkg` | `0EEA2A42800F1FEB78EF8AFAEF946B6789574BEB02A191D9332B0BC9F6617DF8` |
| `NekoLib.Watchdog.1.0.0-local.22.nupkg` | `4B72E2A56935879DC6C718558789787BFD750E03F572E604979FEA376E828440` |
| `NekoLib.Watchdog.Host.1.0.0-local.22.nupkg` | `3222CBE2497664700F2617BD7F3CF9382AAB7A97FF3D44601E77F26E8B9EBBDD` |

## Attempt history and evidence boundary

The first invocation stopped after the `net481` Watchdog test host exited after
startup without producing a test summary. It published no candidate artifacts.
The same isolated Watchdog target then passed 106/106 tests, and the complete
canonical flow was rerun from the same clean commit and same still-unused
version. Only that exit-0 complete rerun qualifies this baseline.

The successful package run is local, manual evidence. It does not substitute
for remote feed publication, CI, a Git tag, gated Phase F2-F7 work, or any
additional manual runtime campaign beyond the package flow's own probes.
