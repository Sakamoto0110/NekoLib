# Data / FarmDatabase

**Kind:** guide

**Lifecycle:** current

**Owner:** `NekoLib.Data` (also exercises `NekoLib.Mvvm` and `NekoLib.Navigation.WinForms`)

**OS / target:** Windows, `net481` and `net9.0-windows`, **x64 only**

**Prerequisites:** Microsoft Access Database Engine 2016 Redistributable (ACE OLEDB),
x64. SQLite needs nothing beyond the restored packages.

**Last verification:** 2026-08-27 — the `--builder` probe passed with exit code 0
on both target frameworks against both providers. Its write-adaptation phase
used lazy schema discovery to promote a string to SQLite `Int64` or Access
`Int32`, reported one lossless `SchemaValidatedRule` hook, and verified that the
stored quantity did not change. The last interactive pass was 2026-08-06:
`net481` against both providers, all seven steps each, and `net9.0-windows` with
SQLite.

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
2. **Tabelas** — the catalog lists six tables: `Animals`, `Employees`, `OperationLog`,
   `Products`, `Roles`, `TagSequence`. Select `Products` and press *Ler tabela*:
   17 rows, 6 columns. The SQL console must show `LIMIT n` on SQLite and `TOP n` on
   Access for the same `Top(n)` call.
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
   When the note is left empty the herd book often fills it in, recording the arrival
   as the offspring of a living female of the same species that is older than it —
   `Filha de BV-002`. The candidate query runs on the transaction's own connection, so
   a female removed a moment earlier can never be credited. It is decorative and
   deliberately not certain, so a run where the note stays empty is not a failure.
7. **Log de operações** — every movement appears newest-first: removals with their
   reason, registrations as `Entrada`.

## Simulation

The *Simulação* page runs the farm by itself. It is an incremental game — plant,
grow, harvest, sell weekly, buy slots and workers, unlock terrains — and it exists
because a game that plays itself is a database load nobody has to click.

Every tick is a transaction. Leaving it running is therefore a sustained write test
against whichever provider is connected, holding the same promise the herd book makes:
the change and its audit row land together or neither does.

**The world market is hidden.** Prices are not a table of constants — each crop has a
world stock, selling into it pushes the price down, and daily consumption pulls it
back up. The month drives that consumption through a prime sequence (1, 2, 3, 5, 7…),
so demand reshuffles every cycle and the farm has to follow. The player never sees the
stock, only the price it produces.

### Headless

A run of thirty thousand ticks cannot be driven through a window, and comparing the
two engines needs the UI out of the way:

```powershell
.\...\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.exe --headless sqlite 7 3000 --checkpoint 500
.\...\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.exe --headless access 7 3000
.\...\NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.exe --headless sqlite 7 1500 --resume
```

It prints a **state digest**: one value covering tick, gold, farm size and every market
quantity. That is what makes provider comparison a string equality rather than a
table-by-table diff, and it is checked against the database on every run — the process
reloads the world at the end and refuses to report success if memory and disk disagree.

`--resume` continues an existing run instead of recreating it, which is how the restart
property is tested.

### What a run checks, and what each exit code means

Every headless run verifies more than it prints. The exit code is the result:

| Code | Meaning |
|---|---|
| 0 | everything held |
| 1 | memory and the database disagree — something never reached disk |
| 4 | an invariant broke; the tick and the rule are on stderr |
| 5 | the economy died — every crop pinned at the price floor |
| 6 | the audit trail does not match the events emitted |
| 7 | `--fault` : the rollback did not roll back |
| 8 | `--stream` : streaming read a different number of rows than `COUNT(*)` |
| 9 | `--reads` : two read shapes disagreed about the same query |
| 10 | `--cancel` : a call ran despite being handed a cancelled token |

**Invariants** run on every tick, in memory: gold non-negative, terrains in range,
slots within capacity, no worker on unbought ground, nothing planted in the future, no
crop outside the catalogue, the worker count matching the board, and no negative market
or inventory. `--no-check` turns them off when only throughput is being measured.

**The audit check** is the direct verification of this scenario's central claim. Every
audited change commits its row in the same transaction as the change, so the
`OperationLog` must grow by exactly the number of events the run emitted. It is checked
by delta, so a resumed run compares against what it found rather than against zero —
and the halves add up: 49 events before a restart plus 80 after equals the 129 of an
uninterrupted run.

### Forcing a transaction to fail

```powershell
.\...exe --headless sqlite 7 3000 --fault 500
```

Advances normally, then makes one transaction fail halfway and proves the database was
left untouched. The failure is a real constraint violation rather than a fabricated
exception: `OperationLog.EntityName` is `NOT NULL` in both dialects, so an event with no
name is rejected by the engine itself. That matters because rollback is the provider's
implementation, not the library's — a synthetic throw would never reach it.

The state row is written before the audit rows, so a rollback that does not work leaves
a tick committed with no audit line beside it.

Judged from the disk alone: after the rollback the snapshot in memory has advanced and
the database has not, so they are supposed to disagree, and comparing them would be the
wrong question.

### Streaming

```powershell
.\...exe --headless sqlite 7 2000 --stream
```

Reads the operation log a row at a time and checks the total against `COUNT(*)`. This is
the one place the two targets genuinely differ, and the difference is deliberate:
`IDatabaseGateway` composes `IDqlStreamingGateway` only on net6 and later; the
streaming interface is absent from the `net481` assembly. On `net481` the run reports
the capability as absent, which is the correct result rather than a gap.

### What the renderer is not

The field drawing observes and never participates; its motion comes from the wall
clock, not from the tick. In accelerated mode it is deliberately *not* faithful — it
exists so the page is not a static image. **It is not evidence.** What a run did is in
the digest and in the operation log.

## Cleanup and side effects

Creates `farm.db` and `farm.accdb` (plus a transient `farm.laccdb`) under
`%LOCALAPPDATA%\NekoLib\FarmDatabase\`. *Recriar o banco do zero* deletes and reseeds
them. Deleting that directory returns the machine to its original state. No ports, no
services, no registry writes.

## Cancellation

```powershell
.\...exe --headless sqlite 1 0 --cancel
.\...exe --headless access 1 0 --cancel
```

Hands every entry point a token that is **already** cancelled and records whether the
call refuses to run. Cancelling mid-flight cannot be arranged deterministically against
a local file database — the queries finish in under a millisecond — so this asks the
narrower question that can be answered exactly: is the token consulted at all? A call
that completes normally with a cancelled token is ignoring it, and no amount of timing
would make that safe.

Every shape refused, on both engines and both target frameworks: session opening, raw,
typed, dynamic, callback, streaming, **and the insert**. Refusing to read is cheap;
refusing to write is the one that protects the database.

This also settles what the fallback mode does in practice. `SynchronousFallbackMode`
defaults to `Disabled`, and the gateway only substitutes a blocking call when a provider
throws `NotSupportedException` *and* the caller opted in. Access runs the whole scenario
with the default, so **OleDb never reaches that path** — the fallback is unexercised by
either provider here, not silently active.

## Every way of reading the same query

```powershell
.\...exe --headless sqlite 1 0 --reads
.\...exe --headless access 1 0 --reads
```

The scenario's original application code used three of the gateway's read shapes and
ignored the rest: the callback overloads, the dynamic path and `ContainsData` had no
coverage at all. Testing each in isolation would prove little, so they are all pointed
at one query and **required to agree on the number of rows**. A shape that disagrees is
either mapping differently or losing rows.

Covered: parameterized `ContainsData` in a populated case plus an empty case,
`GetRaw` from raw SQL and from a builder, `ReadRaw`, `GetDto` both ways, `ReadDto`,
`GetDynamic`, `ReadDynamic`, the same shapes bound to one shared `DbSession`, and on
net6+ `StreamRaw`, `StreamDto` and `StreamDynamic`.

Seventeen shapes on `net9.0-windows`, fourteen on `net481` — the difference is streaming,
as expected. All agree, on both engines, and the typed callback's summed quantity is
identical across all three runs, so they match on values and not only on counts.

## QueryBuilder against both dialects

```powershell
.\...exe --headless sqlite 1 0 --builder
.\...exe --headless access 1 0 --builder
```

Runs every builder clause the scenario's own code never uses, prints the statement each
one became, and fingerprints the rows. The statements are expected to differ between
engines; **the digests are not**. Same builder, different SQL, same answer.

Most of it holds. `Top` becomes `LIMIT 3` against `TOP 3`, `OrderBy` lands on opposite
sides of it, and both return the same rows. `Join`, `GroupBy`, `WhereIn`, `WhereNotIn`,
`WhereBetween` and `WhereLike` emit identical text on both and agree. Subquery parameter
renaming works: a parent using `@p1` pushes the subquery's own `@p1` out to `@p2`.

Two things did not, and both were fixed in `NekoLib.Data` as a result. A third is the
engine's own behaviour and stands as guidance.

### ACE binds subquery parameters out of textual order — fixed

**Was:** the positional binder rewrites placeholders to `?` **in the order they appear
in the text**, which is correct. ACE does not read them that way.

The positional binder rewrites placeholders to `?` **in the order they appear in the
text**, which is correct. ACE does not read them that way. Given

```sql
SELECT [Name] FROM [Products]
WHERE [Quantity] > ? AND EXISTS (SELECT [Id] FROM [Animals] WHERE [Species] = ?)
```

Access answers `Data type mismatch in criteria expression` — it took the string meant
for `[Species]` and compared it against `[Quantity]`. Swapping the two clauses so the
subquery's placeholder comes first makes the same query run, and return exactly what
SQLite returns. SQLite accepts both orderings, because it binds by name.

**It failed loudly only by luck.** One parameter was an integer and the other a string,
so the mismatch surfaced as a type error. Adding a second compatible predicate proved
the rest: Access returned **zero rows where the correct answer was six**, with no error
at all, from the same builder that answered correctly on SQLite. A silently wrong result
on a supported provider.

**Fix:** `QueryBuilder` now emits subquery predicates before the others, so the textual
order matches the order ACE consumes them. Predicates combine with `AND`, so reordering
cannot change the meaning, and a query without a subquery keeps its original SQL
untouched. Both authoring orders now return the same rows on both engines.

Verified for one subquery. Where two or more carry parameters, their relative order is
authoring order and has not been measured.

### DistinctCount emitted SQL Access cannot run — fixed

**Was:** `DistinctCount("[Category]")` produced `SELECT COUNT(DISTINCT [Category])`,
which ACE rejects outright — `Syntax error (missing operator) in query expression`. Jet
and ACE have never supported `COUNT(DISTINCT …)`. The builder offered the method for
both dialects and only one could execute it.

**Fix:** `AccessQueryTranslator` rewrites it as a count over a distinct subselect,
aliased because Jet requires one:

```
SELECT COUNT(*) FROM (SELECT DISTINCT [Category] FROM [Products]) AS [d]
```

Both engines now answer `3`.

### Unaliased aggregates come back under different column names

`COUNT(*)` and `COUNT([Category])` return the same values on both engines, but SQLite
names the result column after the expression while Access invents its own. Anything
reading an aggregate by column name has to alias it.

### One difference that is not a defect

`SelectDistinct(...).Top(2)` translates correctly — Access emits `DISTINCT TOP 2`, which
is the form its own history records as fixed — but the two engines return different
rows. That is the query's fault, not the translation's: a capped distinct set with no
`ORDER BY` has no defined membership.

## Known defects

### The Access catalog sample cannot run

The *Catálogo (Access não expõe por SQL)* sample in the free-query page is dead on
arrival: **ACE OLEDB rejects `--` line comments**, and the sample opens with two of
them. Running it returns

```
Invalid SQL statement; expected 'DELETE', 'INSERT', 'PROCEDURE', 'SELECT', or 'UPDATE'.
```

The comments are prose explaining that Access has no queryable catalog, so the sample
fails for a reason unrelated to the one it is trying to demonstrate. Stripping them
and running `SELECT COUNT(*) AS Produtos FROM [Products]` on its own returns `17`,
which is what isolates the cause to the comments rather than the statement.

The SQLite counterpart carries no comments, which is why this went unnoticed until
`net481` exercised the free-query page against Access for the first time. Defined at
`ViewModels/RawQueryViewModel.cs`, in the `isAccess` branch of the sample list. It is
scenario-local — the gateway dispatched exactly the text it was given.

### FarmButton stops receiving input

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

The 2026-08-06 `net481` pass never reproduced it, which is consistent rather than
contradictory: the window was resized once at the start, and from then on every button
pressed sat on a page that had just been navigated to. That is the documented
workaround applied continuously without meaning to, and it is further evidence that a
fresh layout pass is what clears the condition.

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
| 2026-08-27 | both TFMs | both | **Write-side type adaptation, exit 0 in all four runs.** The builder probe used the same structured `Set` call with the quantity represented as a string. Lazy discovery resolved SQLite `INTEGER` to `Int64` and the Access OleDb column code to `Int32`; each run required exactly one lossless `SchemaValidatedRule` hook and confirmed the persisted value was unchanged. |
| 2026-08-08 | both TFMs | both | **Committed-source revalidation.** The `--builder` probe ran from scenario source `4186e48` against Data fix `865d90f`; all four target/provider pairs exited 0. SQLite and Access returned matching fingerprints for every ordered query, `whereexists` and `exists-antes` both returned 14 rows, the compatible two-predicate variants both returned 6, and Access translated `DistinctCount` to the aliased distinct subquery while returning the same value, `3`. |
| 2026-08-05 | `net9.0-windows` | SQLite | **Interactive pass.** Steps 1–6 driven. Stock movement 240→230 and animal `BV-003` removed, both transactional; log showed both with reasons. |
| 2026-08-05 | `net9.0-windows` | Access (ACE 12.0) | **Interactive pass, core paths.** Steps 1, 2 and 4 driven: `.accdb` created through ADOX and seeded, catalog read from the OleDb schema rowset, `SELECT TOP n` rendered, stock movement 240→250 transactional with positional binding. Steps 3, 5 and 6 not repeated on this provider. |
| 2026-08-05 | `net481` | — | **Build only.** Compiles clean; the executable was never driven. |
| 2026-08-06 | Visual Studio designer | — | **Interactive pass.** `ConnectionPage` and `ReasonPrompt` both opened on the design surface with their layout and custom-painted controls rendered. Opening the prompt is what surfaced the `BeginInvoke`-before-handle defect in the Navigation surface bases, fixed in `73ddbdb`. |
| 2026-08-06 | `net9.0-windows` | SQLite | **Interactive pass on the herd book.** Two cows registered back to back came out as `BV-006 — Filha de BV-001` and `BV-007 — Filha de BV-002`, both mothers living and older, and both notes carried through to the operation log's reason column. |
| 2026-08-06 | `net9.0-windows` | SQLite | **Interactive pass on the registration flow.** `BV-001` and `BV-003` removed with reasons, then a cow registered: it was assigned `BV-006`, leaving the herd at `BV-002, BV-004, BV-005, BV-006` and the log at three entries. The counter read, its update, the insert, the identity read-back and the audit row all landed in one transaction. |
| 2026-08-06 | `net9.0-windows` | SQLite | **Re-run against `378663a`.** The earlier pass predated the Navigation surface-base change, and the removal prompt goes straight through the modified code, so it was driven again: prompt opened centered, background blocked, `BV-001` removed with `DELETE` plus its audit insert. No regression. This run is also where the button defect was observed with maximize and restore swapped, correcting how it is described above. |
| 2026-08-08 | both TFMs | both | **Read shapes and cancellation.** All nineteen of the gateway's read shapes were pointed at one query and required to agree: `ContainsData`, `GetRaw`, `ReadRaw`, `GetDto`, `ReadDto`, `GetDynamic`, `ReadDynamic`, `Get<TTranslator, T>`, `Read<T>`, the same shapes bound to one shared session, and the three `Stream*` variants on net6+. All returned 12 rows on both engines and both targets, with the typed callback's summed quantity identical across every run — agreement on values, not only on counts. A pre-cancelled token was then handed to every entry point including the insert, and every one refused on both engines and both targets. The same measurement shows `SynchronousFallbackMode` defaults to `Disabled` and that neither provider raises `NotSupportedException`, so the blocking-fallback path is never reached here. |
| 2026-08-07 | `net481` | both | **QueryBuilder against both dialects — two library defects found and fixed.** Every builder clause the scenario never used was run on both engines and the results fingerprinted. Access silently returned zero rows where the correct answer was six, because ACE consumes positional placeholders with the subquery's first while the binder emits them in textual order; `QueryBuilder` now orders subquery predicates first and both authoring orders agree on both engines. `DistinctCount` emitted `COUNT(DISTINCT …)`, which ACE cannot parse at all; `AccessQueryTranslator` now rewrites it as a count over an aliased distinct subselect, and both engines answer `3`. The full solution suite passes on both target frameworks after the change, and the scenario's own digests are unchanged. |
| 2026-08-07 | both TFMs | both | **Rollback, streaming and invariants.** A transaction was made to fail halfway on each engine, and each rejected it in its own words — SQLite `NOT NULL constraint failed: OperationLog.EntityName`, Access `You must enter a value in the 'OperationLog.EntityName' field.` — with the state digest and the audit count unchanged on both. This is the scenario's central claim, and until now nothing had ever made one of its `catch { Rollback(); }` blocks run. Streaming read the log row by row on `net9.0-windows` and agreed with `COUNT(*)`; `net481` correctly reported the capability as absent, and both builds compiling is what proves the guard. The same seed produced digest `h249E6267 t2000 g902` on **both target frameworks**, so determinism holds across TFM as well as across provider. Two negative tests were run and reverted to confirm the checkers actually fire: an inelastic world market tripped the economy-dead stop at month 31, and a duplicated worker increment tripped the invariant at tick 630. |
| 2026-08-07 | `net481` | both | **Instrumentation.** `NekoLib.Logging` and `NekoLib.Telemetry` were wired in — the first consumer of either in the repository. Measuring immediately found that persistence was rewriting all 87 board rows on any tick that touched anything: **176 SQL statements per tick**, to save a change that usually moved one tile. Dirty-row tracking cut it to **4.6**, with the digest, restart and cross-provider results all unchanged. The log also showed the database is 1% of wall time at every speed, so the throughput ceiling is the one-pulse-per-second cadence, not the engine: a 500-tick pulse completes in about 15 ms. |
| 2026-08-07 | `net481` | both | **Simulation, headless.** Restart property: 1500 ticks, process exit, `--resume` for 1500 more produced digest `h61C43B17 t3000 g81635` — identical to an uninterrupted 3000-tick run. Cross-provider: seed 11 at 1000 ticks gave `h18B358A1 t1000 g729` on **both** engines. Throughput diverged hard — SQLite 247 tick/s against Access 6 tick/s, a 41× gap on the same work. Two defects were found by the runs themselves and fixed: world consumption was not marked as a state change, so day-boundary market moves could be lost (the digest caught it as a memory/database mismatch), and `AppetiteScale` was set so high that the world out-ate the farm, which meant prices never fell and the decay mechanic never engaged. |
| 2026-08-06 | `net481` | Access (ACE 12.0) | **Interactive pass, steps 1–7.** First full pass on this provider on any target — steps 3, 5 and 6 had never been driven against Access before. `.accdb` recreated through ADOX; catalog listed the six tables **with no statement reaching the console at all**, since it comes from the OleDb schema rowset; `SELECT TOP 100 * FROM [Products]` for the same `Top(n)` that renders `LIMIT 100` on SQLite; both free-query samples returned what SQLite returned (3 rows, and 8 through a single `INNER JOIN`, which ACE accepts unparenthesized); Cenoura 240 → 230 and a 9999 withdrawal refused with nothing emitted; `BV-001` and `BV-003` removed, then a cow registered as `BV-006 — Filha de BV-005`, herd at `BV-002, BV-004, BV-005, BV-006` and `TagSequence` at `BV=6` — identical to SQLite. This run is what surfaced the broken catalog sample described under Known defects. |
| 2026-08-06 | `net481` | SQLite | **Interactive pass, steps 1–7.** First time the `net481` executable was ever driven. Recreated from scratch; catalog read six tables through `sqlite_master`; `Products` at 17×6 with `LIMIT 100`; both free-query samples ran (3 rows, and 8 through an `INNER JOIN`); Cenoura 240 → 230 as `UPDATE` + audit insert in one transaction; a 9999 withdrawal refused with **no statement emitted at all**; removal cancelled once with the herd untouched, then `BV-001` and `BV-003` removed with reasons; a cow registered as `BV-006 — Filha de BV-002`, leaving the herd at `BV-002, BV-004, BV-005, BV-006` and `TagSequence` at `BV=6`; the log listed all four operations newest-first and **not** the refused one. `stdout` and `stderr` both stayed empty for the whole session. |

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

**Registering an animal is one transaction covering six distinct statements**,
including a read-modify-write on the counter and a read-back that only exists because
the gateway cannot return an inserted identity — `Insert` reports affected rows and
nothing else. Captured on `net481` / SQLite, 2026-08-06, with the note left empty, so
the herd book's candidate query ran as well — it sits between the counter read and the
counter update, on the transaction's own connection:

```
despachado  SELECT [LastNumber] FROM [TagSequence] WHERE [Prefix] = @p1
despachado  SELECT [Tag] FROM [Animals] WHERE [Species] = @p1 AND [Gender] = @p2
                                           AND [AgeYears] > @p3 ORDER BY [Tag]
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
