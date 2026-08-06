# Data / FarmDatabase

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Data` (also exercises `NekoLib.Mvvm` and `NekoLib.Navigation.WinForms`)

**OS / target:** Windows, `net481` and `net9.0-windows`, **x64 only**

**Prerequisites:** Microsoft Access Database Engine 2016 Redistributable (ACE OLEDB),
x64. SQLite needs nothing beyond the restored packages.

**Last verification:** 2026-08-05 — interactive on `net9.0-windows`; `net481` builds
but has **not** been driven.

## Purpose

Drive `NekoLib.Data` against two engines that disagree about almost everything the
library has to abstract, using the same application code for both:

| | SQLite | Access (ACE OLEDB) |
|---|---|---|
| Row limit | `LIMIT n` | `TOP n` |
| Parameters | named `@p1` | positional, bound by order |
| Catalog | `sqlite_master`, queryable | OleDb schema rowset only |
| DDL | `INTEGER PRIMARY KEY AUTOINCREMENT`, `TEXT` | `COUNTER`, `TEXT(n)`, `LONG` |
| File creation | on first connect | ADOX catalog, no `CREATE DATABASE` |

The herd also carries a `TagSequence` table — one row per tag prefix holding the last
number issued. New animals draw from it, and it is read, incremented and written
inside the same transaction as the insert, so two registrations cannot agree on the
same tag. Deriving the next number from the surviving rows would not work: a hard
delete takes its own evidence with it, and the whole point is that `BV-003` leaving
does not free that number. A database created before this table existed gets it added
and seeded on connect rather than failing.

It is also the only place in the repository where `NekoLib.Mvvm` is exercised through
a real binding surface, and where Navigation pages are registered **entirely by
attribute** — the shell contains no `ConfigurePages` call.

## Design time

Every page and the removal prompt open in the Visual Studio WinForms designer,
confirmed by opening them rather than by inference. Two things this scenario exposed
are worth knowing before adding a page:

- **Do not put `[DesignerCategory("Code")]` on a page or a surface.** It tells the
  designer the type is code-only and makes it open the editor instead of the design
  view. It belongs on the custom-painted controls under `Theme/`, and nothing warns
  you when it is applied to the wrong kind of type.
- Prompts derive from `ReasonPromptBase`, not from `PromptViewBase<string>` directly,
  because a generic base is the one shape the designer still refuses. Dialogs, toasts
  and popovers need no such shim.

The project multi-targets, and the designer uses the **first** entry in
`TargetFrameworks` — `net481` here, which is the in-proc designer.

## Prerequisites in detail

The ACE driver is an OS install, not a package, and it is registered per bitness: an
x64 install is invisible to an x86 process. The project therefore pins
`<PlatformTarget>x64</PlatformTarget>` rather than running AnyCPU, which on `net481`
also carries the `Prefer32Bit` trap. The two bitnesses cannot be installed side by
side, so x64 is the only supported configuration here.

Databases are created under `%LOCALAPPDATA%\NekoLib\FarmDatabase\`. Nothing is written
inside the repository and there are no tracked fixtures.

## Build

```powershell
dotnet build runtime_tests/Data/FarmDatabase/NekoLib.Data.RuntimeTests.FarmDatabase.WinForms/NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.csproj
```

## Launch

```powershell
.\runtime_tests\Data\FarmDatabase\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms\bin\Debug\net9.0-windows\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.exe
.\runtime_tests\Data\FarmDatabase\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms\bin\Debug\net481\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.exe
```

## Procedure and expected result

Run every step once per provider.

1. **Conexão** — pick the engine, tick *Recriar o banco do zero*, press *Conectar*.
   The status pill shows the provider, the header shows the file path, and the
   dialect notes change with the engine. Both *Conectar* and *Desconectar* must end
   up enabled.
2. **Tabelas** — the catalog lists `Animals`, `Employees`, `OperationLog`,
   `Products`, `Roles`. Select `Products` and press *Ler tabela*: 17 rows, 6 columns.
   The SQL console must show `LIMIT n` on SQLite and `TOP n` on Access for the same
   `Top(n)` call.
3. **Consulta livre** — pick a sample and run it with **Ctrl+Enter** (see the known
   defect below). *Estoque por categoria* returns 3 rows; *Funcionários com cargo*
   returns 8 and exercises an `INNER JOIN`. The sample list itself changes with the
   connected engine.
4. **Controle de estoque** — select a product, set a quantity, press *Saída*. The
   grid updates and the SQL console must show an `UPDATE Products` followed by an
   `INSERT INTO OperationLog`, both inside one transaction. Requesting more than the
   available quantity must be refused before any statement is issued.
5. **Remoção de animal** — select an animal and press *Remover do rebanho*. A modal
   prompt appears, the background is blocked, and *Remover* stays disabled until a
   reason is present. Confirming issues `DELETE FROM [Animals]` plus the audit
   insert, again in one transaction; cancelling leaves the herd untouched.
6. **Registro de animal** — press *Registrar animal*. A second modal collects species,
   gender, age and an optional note, but **no tag**: the database assigns one. Confirm
   and the new arrival must carry the *next* number for its prefix, never a number a
   removed animal used. Remove `BV-001` and `BV-003` first and the herd should end up
   reading `BV-002, BV-004, BV-005, BV-006` — the gaps are the point.
7. **Log de operações** — every movement appears newest-first: removals with their
   reason, registrations as `Entrada`.

## Cleanup and side effects

Creates `farm.db` and `farm.accdb` (plus a transient `farm.laccdb`) under
`%LOCALAPPDATA%\NekoLib\FarmDatabase\`. *Recriar o banco do zero* deletes and reseeds
them. Deleting that directory returns the machine to its original state. No ports, no
services, no registry writes.

## Known defect

**Some `FarmButton` instances stop receiving mouse input after the window changes
size or state, until another layout pass happens.** They render correctly and report
as enabled, but receive nothing — not even hover.

It is *not* tied to maximizing, which an earlier revision of this file claimed.
Observed both ways:

| Page | Button | Maximized | Restored |
|---|---|---|---|
| Consulta livre | *Executar*, *Limpar* | dead | works |
| Controle de estoque | *Remover do rebanho* | works | dead |

Every non-`FarmButton` control — grids, combos, the sidebar, the numeric input — kept
working in every state, which is what points at the custom-painted button rather than
at the containers.

Workarounds that reliably recover a dead button: toggle the window state, or navigate
away and back. **Ctrl+Enter runs the query in every state**, so the Consulta livre
page is fully verifiable regardless.

Ruled out so far: `Anchor` versus docking, the `SplitContainer` that originally hosted
the editor, the dock direction of the button bar, and stale paint — an `Invalidate` on
every layout pass did not fix it, though it did prove the controls move position on
repaint, so what is drawn and what is clickable can disagree.

Unresolved. It affects the scenario's ergonomics, not the database behaviour under
test, and it is scenario-local: the button is defined in `Theme/FarmControls.cs`, not
in any NekoLib module.

## Verification record

| Date | Target | Provider | Result |
|---|---|---|---|
| 2026-08-05 | `net9.0-windows` | SQLite | **Interactive pass.** Steps 1–6 driven. Stock movement 240→230 and animal `BV-003` removed, both transactional; log showed both with reasons. |
| 2026-08-05 | `net9.0-windows` | Access (ACE 12.0) | **Interactive pass, core paths.** Steps 1, 2 and 4 driven: `.accdb` created through ADOX and seeded, catalog read from the OleDb schema rowset, `SELECT TOP n` rendered, stock movement 240→250 transactional with positional binding. Steps 3, 5 and 6 not repeated on this provider. |
| 2026-08-05 | `net481` | — | **Build only.** Compiles clean; the executable was never driven. |
| 2026-08-06 | Visual Studio designer | — | **Interactive pass.** `ConnectionPage` and `ReasonPrompt` both opened on the design surface with their layout and custom-painted controls rendered. Opening the prompt is what surfaced the `BeginInvoke`-before-handle defect in the Navigation surface bases, fixed in `73ddbdb`. |
| 2026-08-06 | `net9.0-windows` | SQLite | **Interactive pass on the registration flow.** `BV-001` and `BV-003` removed with reasons, then a cow registered: it was assigned `BV-006`, leaving the herd at `BV-002, BV-004, BV-005, BV-006` and the log at three entries. The counter read, its update, the insert, the identity read-back and the audit row all landed in one transaction. |
| 2026-08-06 | `net9.0-windows` | SQLite | **Re-run against `378663a`.** The earlier pass predated the Navigation surface-base change, and the removal prompt goes straight through the modified code, so it was driven again: prompt opened centered, background blocked, `BV-001` removed with `DELETE` plus its audit insert. No regression. This run is also where the button defect was observed with maximize and restore swapped, correcting how it is described above. |

A separate throwaway console harness exercised the whole `Core` surface against both
providers before any UI existed, and passed on both — including the negative cases
(stock refused below zero, removal refused without a reason). That harness was not
kept: everything it covered is reachable from the steps above.

## Observed evidence

Captured from the in-app SQL console during the 2026-08-05 pass. This is the reason
the scenario exists, so it is recorded rather than summarized.

**The same `Top(n)` call renders differently per engine.** Both rows come from
pressing *Ler tabela* with the row cap enabled:

```
SQLite   SELECT * FROM [Animals] LIMIT 100
Access   SELECT TOP 1004 * FROM [Products]
```

**A stock movement emits its update and its audit row as one unit.** Identical shape
on both providers; on Access the `@pN` markers are bound positionally by the OleDb
binder rather than by name:

```
gerado      UPDATE Products SET Quantity = @p2 WHERE [Id] = @p1
despachado  UPDATE Products SET Quantity = @p2 WHERE [Id] = @p1
gerado      INSERT INTO OperationLog (OccurredAt, EntityKind, EntityId, EntityName,
                                      Operation, Quantity, Reason)
            VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7)
despachado  INSERT INTO OperationLog (...) VALUES (...)
```

**Removing an animal pairs a raw delete with the same audit insert**, because
`Delete` has no `QueryBuilder` overload — only the raw-SQL one:

```
despachado  DELETE FROM [Animals] WHERE [Id] = @p1
gerado      INSERT INTO OperationLog (...) VALUES (@p1, ..., @p7)
despachado  INSERT INTO OperationLog (...) VALUES (...)
```

**Registering an animal is seven statements in one transaction**, including a
read-modify-write on the counter and a read-back that only exists because the gateway
cannot return an inserted identity — `Insert` reports affected rows and nothing else:

```
despachado  SELECT [LastNumber] FROM [TagSequence] WHERE [Prefix] = @p1
gerado      UPDATE TagSequence SET LastNumber = @p2 WHERE [Prefix] = @p1
despachado  UPDATE TagSequence SET LastNumber = @p2 WHERE [Prefix] = @p1
gerado      INSERT INTO Animals (Species, Tag, AgeYears, Gender, Notes)
            VALUES (@p1, @p2, @p3, @p4, @p5)
despachado  INSERT INTO Animals (...) VALUES (...)
despachado  SELECT [Id] FROM [Animals] WHERE [Tag] = @p1
gerado      INSERT INTO OperationLog (...) VALUES (@p1, ..., @p7)
despachado  INSERT INTO OperationLog (...) VALUES (...)
```

Measured outcomes: Cenoura 240 → 230 on SQLite and 240 → 250 on Access; `BV-003`
left a 14-animal herd; the operations page then listed both movements newest-first
with their reasons. On the 2026-08-06 run, removing `BV-001` and `BV-003` and then
registering a cow produced a herd reading `BV-002, BV-004, BV-005, BV-006` and a log
of three entries — one `Entrada`, two `Saída` — which is the numbering rule holding
end to end.

The console shows statements at all only because the scenario opts into
`DatabaseGatewayOptions.EmitRawSqlInEvents` — the library's default redacts them to
`[SQL redacted]`.
