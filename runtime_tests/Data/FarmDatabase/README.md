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

It is also the only place in the repository where `NekoLib.Mvvm` is exercised through
a real binding surface, and where Navigation pages are registered **entirely by
attribute** — the shell contains no `ConfigurePages` call.

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
6. **Log de operações** — both movements appear newest-first, each carrying the
   reason recorded with it.

## Cleanup and side effects

Creates `farm.db` and `farm.accdb` (plus a transient `farm.laccdb`) under
`%LOCALAPPDATA%\NekoLib\FarmDatabase\`. *Recriar o banco do zero* deletes and reseeds
them. Deleting that directory returns the machine to its original state. No ports, no
services, no registry writes.

## Known defect

**The *Executar* and *Limpar* buttons on the Consulta livre page do not respond to
clicks while the window is maximized.** They render correctly and report as enabled,
but receive no mouse input — not even hover. Un-maximized they work normally, and
**Ctrl+Enter runs the query in both states**, so the page's behaviour is fully
verifiable.

Four causes were investigated and ruled out: `Anchor` versus docking, the
`SplitContainer` that originally hosted the editor, the dock direction of the button
bar, and stale paint (an `Invalidate` on layout did not fix it, though it did prove
the controls move on repaint). The equivalent buttons on the Controle de estoque page
share the same container structure and work. Unresolved; it affects the scenario's
ergonomics, not the database behaviour under test.

## Verification record

| Date | Target | Provider | Result |
|---|---|---|---|
| 2026-08-05 | `net9.0-windows` | SQLite | **Interactive pass.** Steps 1–6 driven. Stock movement 240→230 and animal `BV-003` removed, both transactional; log showed both with reasons. |
| 2026-08-05 | `net9.0-windows` | Access (ACE 12.0) | **Interactive pass, core paths.** Steps 1, 2 and 4 driven: `.accdb` created through ADOX and seeded, catalog read from the OleDb schema rowset, `SELECT TOP n` rendered, stock movement 240→250 transactional with positional binding. Steps 3, 5 and 6 not repeated on this provider. |
| 2026-08-05 | `net481` | — | **Build only.** Compiles clean; the executable was never driven. |

A separate throwaway console harness exercised the whole `Core` surface against both
providers before any UI existed, and passed on both — including the negative cases
(stock refused below zero, removal refused without a reason). That harness was not
kept: everything it covered is reachable from the steps above.
