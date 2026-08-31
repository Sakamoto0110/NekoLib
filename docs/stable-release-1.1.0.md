# NekoLib 1.1.0 Stable Family Release

**Kind:** reference

**Lifecycle:** current

**Subject:** first compatible minor of the coordinated stable family

**Declaration date:** 2026-08-31

**Qualifying implementation commit:**
`d1bf43a89232bb2b2626009248f1a511ed5ae854`

**Qualifying package candidate:** `1.1.0-local.9`

**Materialized release source commit:**
`1147f76beb412c3ae6368088bc0c22eb4653daa8`

**Materialized package version:** `1.1.0`

## Declaration boundary

NekoLib `1.1.0` is the approved first compatible minor after the coordinated
`1.0.0` stable baseline. The SemVer-minor justification is the additive stable
`NekoLib.Data` surface: structured QueryBuilder operations, warning-only legacy
overloads, explicit write-side promotion and provider decay, and exact DTO
temporal materialization rules.

The release also carries the compatible Logging flush-budget correction and
package-owned managed XML documentation across all 15 library packages. It
does not remove a stable API, change the warning-only QueryBuilder compatibility
window, open `2.0.0`, promote `NEKOEXP0001`, or change the Watchdog Host protocol
or deployment topology.

The qualifying implementation commit contains the intended product, public API,
XML documentation, module-first documentation, indexing, and packaging inputs.
The materialized release source commit adds only the coordinated release
changelogs and this record. Both immutable package sets were produced from that
clean source, and every main-package nuspec records that exact repository
commit.

## Compatibility and migration

| Area | Classification | Consumer action |
|---|---|---|
| Structured QueryBuilder | additive stable API plus warning-only deprecations | Move new code to `Value`, `Set`, structured `Where`, `JoinOn`, and the explicitly trusted variants; existing overloads continue to compile in `1.1.0` |
| Data type adaptation | additive stable API and explicit behavioral policy | Configure exact promotion, decay, loss, schema, and DTO-property rules where adaptation is required |
| Logging flush admission | compatible behavioral correction | none |
| Managed XML assets | documentation and packaging | none; IDEs receive the package-owned XML beside each target assembly |
| Watchdog Host | coordinated release only | Upgrade `NekoLib.Watchdog` and `NekoLib.Watchdog.Host` together |

Migration guidance:

- [Structured QueryBuilder migration](modules/Data/migrations/querybuilder-structured-api.md)
- [Data type-adaptation migration](modules/Data/migrations/data-type-adaptation.md)

The 30 accepted compiled API baselines are not updated by the release operation.
They already contain the accepted additive Data surface and warning-only
`Obsolete` markers. Qualification must fail on any unexplained difference.

## Qualification plan and evidence boundary

The complete gate is executed on Windows against one clean source commit:

1. complete Release build and solution tests;
2. warning-identity comparison through `eng/verify-docs.ps1` with the captured
   rebuild log;
3. all 30 accepted public API manifests;
4. documentation, skills, and diff verification;
5. immutable `1.1.0-local.9` through the complete canonical package flow;
6. exact stable `1.1.0` through the same flow;
7. isolated PackageReference consumers and Watchdog Host package/runtime probes;
8. individual and aggregate SHA-256 recording before any remote publication.

| Evidence layer | Qualified release state |
|---|---|
| Source and documentation | Clean source `1147f76beb412c3ae6368088bc0c22eb4653daa8`; documentation, skills, premises, and diff verification passed |
| Build and warnings | Release rebuild passed with 202 warning occurrences, 0 errors, and no new normalized warning identity |
| Solution tests | 1,787 passed, 0 failed, 0 skipped |
| Compiled public API | All 30 accepted manifests passed without baseline changes |
| Package and managed XML | `1.1.0-local.9` and exact `1.1.0` each passed the canonical flow; 16 main and 15 symbol artifacts; every managed package carries XML for both target families |
| PackageReference consumers | WinForms and WPF consumers passed on `net481` and `net9.0-windows` for both package sets |
| Watchdog Host deployment/runtime | Package topology, unsupported `win-arm64` negative probe, protocol mismatch, startup, and stop passed on both target families for both package sets |
| Interactive/native hardware/provider/soak | not added by this release gate; prior evidence remains scoped to its recorded source |
| Git | `origin/master` includes the release evidence; annotated `v1.1.0` resolves to source `1147f76beb412c3ae6368088bc0c22eb4653daa8` |
| GitHub Release | Public [`v1.1.0`](https://github.com/Sakamoto0110/NekoLib/releases/tag/v1.1.0), release ID `380040305`, contains exactly 31 assets; GitHub digests recompute the approved stable aggregate |
| NuGet.org | All 16 package IDs expose `1.1.0`; fresh repository-signed downloads passed signature, content, and complete external-consumer verification |

The complete local commands were:

```powershell
dotnet build NekoLib.sln -c Release -t:Rebuild -m:1
.\eng\verify-docs.ps1 -BuildLogPath artifacts/release-1.1.0/rebuild.log
.\eng\verify-public-api.ps1 -NoBuild
.\eng\verify-skills.ps1
.\eng\verify-premises.ps1
dotnet test NekoLib.sln -c Release -m:1 --no-build --no-restore
.\eng\pack-local.ps1 -PackageVersion 1.1.0-local.9
.\eng\pack-local.ps1 -PackageVersion 1.1.0
```

The candidate aggregate SHA-256 over the sorted UTF-8 sequence
`filename=SHA256\n` is:

```text
ACFDE23661FCFD53B2320CA8AB7CA0D61BB32613C915F35EA314B1E14C25251B
```

The exact stable package aggregate, calculated by the same algorithm over all
31 artifacts, is:

```text
C3E0764831636A74CD4969BDC609B16EAEF86FBE0A375C6741217F0F386E7F43
```

### Exact stable artifact hashes

| Artifact | SHA-256 |
|---|---|
| `NekoLib.Core.1.1.0.nupkg` | `A5BAEA52D56F80C20AB05A59322429226AA0163AA315122DBC023F44E5B8D906` |
| `NekoLib.Core.1.1.0.snupkg` | `1F2159D9221D4F85D3085243338CC7E0A0EEB9BF3032266A2B28988769F5F0F8` |
| `NekoLib.Data.1.1.0.nupkg` | `1259027E6389A8E3F1D0587F1D845F9D2C9BB91EEF555EA3AF041A13E60CB4A7` |
| `NekoLib.Data.1.1.0.snupkg` | `187B558F8F5C254D06CB8A41A334E42A68B5745D0D36C2414921F0EDB772EEF0` |
| `NekoLib.Devices.1.1.0.nupkg` | `24FA9CF37D0B4CE3A756FEC07E080E2500B20569AF0A8344ECFE062333BBD067` |
| `NekoLib.Devices.1.1.0.snupkg` | `BD16B6E6F024B1BDAC958F4B72966FB161F09BDB06BDB0500D4E3F1E74AF2739` |
| `NekoLib.Diagnostics.1.1.0.nupkg` | `1D827F5061BFDAAC21B4726F7B6547144A55B423685E40FE73F653FA6EA410F1` |
| `NekoLib.Diagnostics.1.1.0.snupkg` | `B44603E0FB5CE9AE439533D50B4FE7BC09D5CD188A421538BC1E04B7D82A9C0B` |
| `NekoLib.Diagnostics.Windows.1.1.0.nupkg` | `64A08889A3A5FB347A0BA3D85EBB9BCEC34B573AE07BB1C5F5F134F5DCFFB3F2` |
| `NekoLib.Diagnostics.Windows.1.1.0.snupkg` | `093B075AEC55147763CE07FA048BCF1D8DAFEC9D7F5CB044843D634518F3698A` |
| `NekoLib.Http.1.1.0.nupkg` | `5A6572289E526B9E93D9C1D993B62FE69094AEB0BF335586B2E5A9BB15D1681B` |
| `NekoLib.Http.1.1.0.snupkg` | `8DB3085DC53614C5ACCAD65DA7C07F9A962BC782B8408CCA288590A53C4D745F` |
| `NekoLib.Inspection.1.1.0.nupkg` | `5FC4480786BB4A4921353D87AB55667D2760AC5FE2CAD1DBEBF46368898DEF75` |
| `NekoLib.Inspection.1.1.0.snupkg` | `5E4B7B2A0C03989B6A656DCBD44E52237C0F2F4348CFC28E7959F384A307E596` |
| `NekoLib.Logging.1.1.0.nupkg` | `01D777B4A3A12A6F7D1A0F79209182A8B13196911A017D4B67DA0655FB119303` |
| `NekoLib.Logging.1.1.0.snupkg` | `26A929497C04DDA0D00BC62C58857E5FCB5FF5E58CDAD9C7260694649EF0672F` |
| `NekoLib.Mvvm.1.1.0.nupkg` | `536FCEB26E4586053871E6C1ABB3F4788099D8F77A134E10E18A390B8A7D5892` |
| `NekoLib.Mvvm.1.1.0.snupkg` | `AB1C7B0312D0573506CFA5D05AB1A36441C9D98097AF575C17646D8A23D06956` |
| `NekoLib.Navigation.1.1.0.nupkg` | `F5D7152463A869AE1DF6931800EF8532D7D81C14B1DA2A47F70372356FA3D6E6` |
| `NekoLib.Navigation.1.1.0.snupkg` | `D34BCB3F435A8660DA841E4F40278CC5B0E18D23DEDEE61E7391BF669479A01A` |
| `NekoLib.Navigation.WinForms.1.1.0.nupkg` | `B3BDC33973A766E6A08A1F8C9B43B5B957CAC5F815950063B0E0792EE2B176DB` |
| `NekoLib.Navigation.WinForms.1.1.0.snupkg` | `ACA68447ADF548AFFDB94798B2689130F0D009FACCFA5BAFB563A727F2E53D2C` |
| `NekoLib.Navigation.Wpf.1.1.0.nupkg` | `7C85FB030ACB45E36F60D9E571D8ACF963B5482ABD10C3443E848E2570E06334` |
| `NekoLib.Navigation.Wpf.1.1.0.snupkg` | `E5849710E15FFBF3383361247F82BC92DEA19ADC87D459A5B999F475977AC465` |
| `NekoLib.Pipes.1.1.0.nupkg` | `4892461E8E897CAC7106AF58C07668C4FB4DC6A651054FED74120DE1A950CB86` |
| `NekoLib.Pipes.1.1.0.snupkg` | `A997B9C4F5CBB242DEF9367FDEC178706F3DA6D38EA405BEF892C32DFA3BB868` |
| `NekoLib.Telemetry.1.1.0.nupkg` | `9CFCC3CBC9942175CFB3245F2C79C85808347FE6FBD526C052EA0DC89CD35D35` |
| `NekoLib.Telemetry.1.1.0.snupkg` | `9A28F20F4665FDAD575F53B034F90A4A62BFBD12026E1959FF24D5BEF49CE547` |
| `NekoLib.Watchdog.1.1.0.nupkg` | `2D1300F34D0A384C8A0E816F243CEC4F3691020E3EFE1B0397D672BEABEBD4EB` |
| `NekoLib.Watchdog.1.1.0.snupkg` | `0551A59941884CFCC777DF0E7478CDC68CE3CFA7F3EBDD19616C8FBD0E410AE0` |
| `NekoLib.Watchdog.Host.1.1.0.nupkg` | `FC513919CBB1EB8B8351F0038D15DDC8CD3E2D6EEDD8047C0AD598F42F016456` |

The stable and candidate ZIP entry sets are identical after filename-version
normalization. Content differences are limited to version-bearing nuspec/core
properties, assemblies, symbols, executables, and Host dependency metadata;
XML, README, build assets, and other static package content are byte-identical.

## Publication evidence

Remote publication used the manual-only
`.github/workflows/publish-nuget.yml` trusted-publication transport bound to
draft release ID `380040305`, annotated tag `v1.1.0`, and aggregate
`C3E0764831636A74CD4969BDC609B16EAEF86FBE0A375C6741217F0F386E7F43`.
The successful [workflow run `33437988158`](https://github.com/Sakamoto0110/NekoLib/actions/runs/33437988158)
verified the 31 downloaded assets before requesting an OIDC credential, then
received `Created` for all 16 main and 15 symbol packages.

Run `33437837425` preceded it and stopped safely while reading the draft release
with `Resource not accessible by integration`. Hash verification, OIDC login,
and package publication were skipped, so no partial publication occurred. The
workflow then received the GitHub permission required to read draft assets;
after the successful run, repository-token access was reduced to read-only
again.

After all 16 versions appeared in the NuGet.org flat container, each main
package was downloaded into a fresh local feed and checked with:

```powershell
dotnet nuget verify --all <downloaded-package>
.\eng\test-local-packages.ps1 `
    -PackageVersion 1.1.0 `
    -FeedPath artifacts\release-1.1.0\public-packages
```

All 16 repository signatures passed. ZIP-entry comparison proved that every
entry from the approved GitHub/local `.nupkg` is byte-identical in the public
download and that `.signature.p7s` is the only added entry. The external flow
then passed PackageReference-only WinForms and WPF consumers on both target
families, multi-target and transitive Host checks, the expected unsupported
`win-arm64` failure, both packaged protocol-mismatch probes, both startup/stop
probes, deployment opt-out/re-enable, and cleanup.

NuGet.org repository signing intentionally changes each outer main-package
hash. The independently downloaded signed hashes are:

| Public signed package | SHA-256 |
|---|---|
| `NekoLib.Core.1.1.0.nupkg` | `D9AD994F99CB7BB7BBE44465D5E9AD719671FCB593B7389D8358DCFBE0BE558D` |
| `NekoLib.Data.1.1.0.nupkg` | `F50B193DD2213F11D0925259819A77754892438934FE9BA803C3DE3AB3A7FA68` |
| `NekoLib.Devices.1.1.0.nupkg` | `74DB70FA8782E691522189FC7B29C708ADFB31E97FB8197F167C3EC117982185` |
| `NekoLib.Diagnostics.1.1.0.nupkg` | `39EBE5F43C471A6C4D34D0ADF54F1CE69259E339DFB65AD63BDDE3C3495D6AC7` |
| `NekoLib.Diagnostics.Windows.1.1.0.nupkg` | `9BB6F0D45FD1AB74DD036637C2C5B14FAD2ABA48BA9079FEC9EFC649B191FE69` |
| `NekoLib.Http.1.1.0.nupkg` | `0D7736D5707875A9B50D1DC216A0BCB831BC744141E38EA09B5C1F4D604E3B8A` |
| `NekoLib.Inspection.1.1.0.nupkg` | `EA1F13DECDE1C17E8A72EA491B2550AE4B91B3953D9F3CF5626AD8D2E1B63FD5` |
| `NekoLib.Logging.1.1.0.nupkg` | `F14BD240E63800B4369AF3903ABDF751C6D513A43B60A7F874CE018EB26DAF53` |
| `NekoLib.Mvvm.1.1.0.nupkg` | `2E5C27D06F3C5C8E2DD7D1EC72DD6DEB56A624E43AA175B08F4E9848D08E2D18` |
| `NekoLib.Navigation.1.1.0.nupkg` | `3C4E891CF208E0A3F0715C112FAD0D72A72CEAF6E7CA7E936BFA2AFCF88FE1EA` |
| `NekoLib.Navigation.WinForms.1.1.0.nupkg` | `F2FDB8AB6DA645AD6073E43FE9D1F5DBC0ECECD605FA9559A447BC90CC9CFD05` |
| `NekoLib.Navigation.Wpf.1.1.0.nupkg` | `6D8ACAC88EAD3028DC7A1973BAEC6590D5E42713B68DD972420BA18794B9552D` |
| `NekoLib.Pipes.1.1.0.nupkg` | `EBDB3DA4A698F611B2FC56109B356C315891D694A86C4302EEAB5B84710711AE` |
| `NekoLib.Telemetry.1.1.0.nupkg` | `5F7C9A54959009612775AECFCCDDE4C17ACD6F3641C7DF726FFE1D88EE6F8C6D` |
| `NekoLib.Watchdog.1.1.0.nupkg` | `371418BA647E0DE21B904F53858D61D2BD3036F0A855117433DDCD149ED2B5F6` |
| `NekoLib.Watchdog.Host.1.1.0.nupkg` | `24324F302A130EFC09D4DF65918F68F5885DF2FBB2A7626BE546A347BD8EB68D` |

The GitHub Release preserves the unsigned approved bytes and all 31 original
hashes. Public NuGet.org symbols were accepted by the successful workflow but
are not exposed by the main-package flat-container endpoint; the workflow's
`Created` responses are the retained remote symbol-publication evidence.
