# NekoLib 1.0.0 Stable Family Baseline

**Kind:** reference

**Lifecycle:** current

**Subject:** first stable coordinated family release and support baseline

**Declaration date:** 2026-08-21

**Qualifying source commit:**
`7090e40eed7c6b888ce8da732f21cbe10f1a936c`

**Qualifying package candidate:** `1.0.0-local.22`

**Materialized release source commit:**
`db63529cafce11690a18a595e4abc6c0610b9b8e`

**Materialized package version:** `1.0.0`

## Declaration

NekoLib `1.0.0` is the first stable coordinated family support baseline. The
30 accepted compiled public API manifests and the accepted Watchdog Host
deployment contract at the qualifying source commit are the initial stable
baselines under the
[`public API and release policy`](public-api-release-policy.md).

All accepted unmarked public and protected surfaces are stable from `1.0.0`.
`IInspectionRecorder.RegisterAction` remains explicitly experimental under
`NEKOEXP0001`; this declaration does not promote it.

The declaration records a support and compatibility boundary. The annotated
`v1.0.0` Git tag points exactly to materialized package-source commit
`db63529cafce11690a18a595e4abc6c0610b9b8e`, and the release history has been
pushed to GitHub. No `1.0.0` package has been pushed to a remote package feed,
and no GitHub Release has been created.

## Remote publication transport

The manual-only `.github/workflows/publish-nuget.yml` workflow is the approved
transport for this package set. It can run only when explicitly dispatched from
`master` with publication confirmed, uses the GitHub `release` environment, and
has no push, pull-request, or scheduled trigger. It downloads the exact package
assets attached to the `v1.0.0` GitHub release and rejects the set before login
unless the 16 `.nupkg`, 15 `.snupkg`, and aggregate SHA-256 match this record.
Only then does it request a short-lived NuGet.org credential through OIDC and
push the main packages and adjacent symbols.

The workflow and trusted-publishing policy are publication infrastructure, not
publication evidence. This record must retain the no-remote-package boundary
until NuGet.org accepts the packages and an external restore verifies them.

## Materialized stable package evidence

The coordinated stable package set was produced locally from the clean
materialized release source commit without `-SkipTests`, `-AllowDirty`, or
skipped package probes:

```powershell
.\eng\pack-local.ps1 -PackageVersion 1.0.0
```

| Gate | Result |
|---|---|
| Source provenance | Clean commit `db63529cafce11690a18a595e4abc6c0610b9b8e`; all 16 `.nupkg` nuspecs record version `1.0.0` and that repository commit |
| Build | 202 baseline warning occurrences and 0 errors, matching the qualifying Release rebuild count |
| Solution tests | 1,670 passed, 0 failed, 0 skipped |
| Coordinated packages | 16 `.nupkg` and 15 `.snupkg` artifacts retained in the local feed |
| Watchdog Host payloads | `net481`, `net9.0-windows7.0/win-x86`, and `net9.0-windows7.0/win-x64` present and validated |
| External consumers | PackageReference-only WinForms and WPF consumers passed on `net481` and `net9.0-windows`; consumer builds emitted 0 warnings and 0 errors |
| Host negative probe | Unsupported `win-arm64` deployment failed as required |
| Host runtime probes | Packaged protocol mismatch, startup, and stop probes passed on both target families |
| Cleanup | Package, Host staging, and consumer-smoke session directories were removed; no repository-owned process remained |

The deterministic aggregate SHA-256 over the sorted UTF-8 sequence
`filename=SHA256\n` for all 31 retained stable package artifacts is:

```text
3E24185B9246BDB20BDE96C188CA67CAD2603209B861BF0C1A4D1889CBD72887
```

### Stable main package hashes

| Package | SHA-256 |
|---|---|
| `NekoLib.Core.1.0.0.nupkg` | `0FD3B8300BAFCE13A7AA920FA07C4ED5170B10F1B846E022EB7851662CB907A6` |
| `NekoLib.Data.1.0.0.nupkg` | `962F4F4F6E47AAEFDD552E97CE718B86B3FDCF62A49293913FB89308B8CCE96A` |
| `NekoLib.Devices.1.0.0.nupkg` | `52CEAEDE96933EF0C9D63F9117B68B659534ACB020445D9A19F072CFF93A17A3` |
| `NekoLib.Diagnostics.1.0.0.nupkg` | `8AC403215EBC0F1C465D2B1CC6AE744E6B4B010878DFA5E304E58F049B240F80` |
| `NekoLib.Diagnostics.Windows.1.0.0.nupkg` | `F59010D2225A6687BE9C0F96AA8167BB0B1E27B3F5E8A6260D966A3F33DAAE59` |
| `NekoLib.Http.1.0.0.nupkg` | `06824B22FD43EBF68C225F6B89C16903E4572AE654304D3FC4FCFA8DCB603601` |
| `NekoLib.Inspection.1.0.0.nupkg` | `18664A0D3523CA2AB1DAEE5DE43F6F9E7EADC72B14E283BC564A33DB633CE9B3` |
| `NekoLib.Logging.1.0.0.nupkg` | `8183024F0130A0917DDBC4936F742F4246B22B62D383461BD2439797655E48BC` |
| `NekoLib.Mvvm.1.0.0.nupkg` | `AD76F8F1A6F7C09B4F5947643FFC58F7D772EF453CD12C1E1430DF4F5C9B9496` |
| `NekoLib.Navigation.1.0.0.nupkg` | `BDA72193BCA46B4F6C264D373E4A72B09D5BF00260086F636AF986B176CE4E75` |
| `NekoLib.Navigation.WinForms.1.0.0.nupkg` | `AEE1948E9163D13FC64DF1014283067BCDCCE8F0E04A24A3B36008E866EE9CED` |
| `NekoLib.Navigation.Wpf.1.0.0.nupkg` | `C54FC1E66F6D422E0E0665E46BDC3A084C739803F979D6CB29FA5A9D96B44486` |
| `NekoLib.Pipes.1.0.0.nupkg` | `D147D5142515BAA821512F294F5C6763D0D9494A4FB14AA60DC2AAB12D386B89` |
| `NekoLib.Telemetry.1.0.0.nupkg` | `CC9720739B4974B26FC1AF6A6B7488BEBC0A328D132ECBB4EA2424A4536158E8` |
| `NekoLib.Watchdog.1.0.0.nupkg` | `4880C6AFDBC8D4D54A7D3040E35F9DFADD35F88EA3E8B44080A3D86A56BA8FE0` |
| `NekoLib.Watchdog.Host.1.0.0.nupkg` | `914DD04CC5808BE8E314EE64D4004551F1C9AF26A26E6BAEF69983361BBE7230` |

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

## Qualifying candidate main package hashes

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

The successful package run is local, manual evidence. The later tag and branch
publication preserve its provenance but do not substitute for remote package
feed publication, CI, gated Phase F2-F7 work, or any additional manual runtime
campaign beyond the package flow's own probes.
