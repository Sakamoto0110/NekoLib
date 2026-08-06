# Navigation Design-Time Loadability — 2026-08-06

**Kind:** audit

**Lifecycle:** current

**Subject:** whether a consuming application can lay Navigation pages and overlay
surfaces out in the Visual Studio WinForms designer, and what had to change in
`NekoLib.Navigation.WinForms` and `NekoLib.Navigation.Wpf` for that to hold

**Reference date:** 2026-08-06

**Reference commit:** `5418cb27f8da669a060ac382fa277c59d2322769`

**Last reconciliation:** not yet reconciled

**Current state:** both accepted findings are implemented and locked by
`SurfaceBaseDesignTimeTests`; one deliberate residual gap remains and is not
scheduled — see the residual-gaps section

## How this review was produced

Not by reading the module. The findings came out of building a real consuming
application against it — the `runtime_tests/Data/FarmDatabase` scenario — and then
opening its pages and its prompt in the Visual Studio 2026 WinForms designer. Both
defects below are invisible to a compiler, to the test suite as it stood, and to
anyone reading the source without a designer open.

This is a continuation of a concern this repository has recorded before.
[`navigation-audit.md`](navigation-audit.md) closes with a *Designer-split restore —
HYGIENE* section: during Pass 5 the demo pages could not be opened in the designer
because they lacked the `partial class` + `*.Designer.cs` split. That was fixed for
the demo pages. Nobody checked the framework's own base classes, and the base classes
were the harder half.

## Finding 1 — every surface base was `abstract`

### Observed

All five overlay bases were declared `abstract`, on both platforms:

| Type | WinForms | WPF |
|---|---|---|
| `DialogViewBase` | abstract | abstract |
| `PromptViewBase<TResult>` | abstract | abstract |
| `PopoverViewBase` | abstract | abstract |
| `ToastViewBase` | abstract | abstract |
| `AutoDismissPopoverBase` | abstract | abstract |

A visual designer instantiates the **base class** of the type it is opening in order
to render the subclass on the design surface. An abstract base therefore makes every
subclass undesignable — the designer reports that it cannot load the type and falls
back to the code editor.

### Why they were abstract

No commit message, README, or `TODO.md` entry records abstractness as a decision. The
bases entered the tree inside `639ecf2`, a large mixed commit, and
`a59f0dd` added the popover pair afterwards following the existing shape. All five
carry identical scaffolding — the same shadowed `DesignMode` property, the same
`Name = GetType().Name` line with the same `NAV-008(g)` comment, the same protected
constructor — so they are template siblings rather than five independent decisions.

The most probable reason is intent signalling: `abstract` on a `*Base` type is the
idiomatic C# way to say "do not instantiate this directly". That is a reasonable
reflex. It simply collides with how a visual designer works, which is not something
the reflex accounts for.

### Why removing it is safe

**The intent survives without the modifier.** Every one of these bases already
declares a `protected` constructor, which means external code cannot instantiate it
regardless. This was confirmed accidentally and then deliberately: an early draft of
the regression test wrote `new DialogViewBase()` from the test assembly and the
compiler rejected it with `CS0122 — inaccessible due to its protection level`. The
final test derives a probe type instead, which is what a consumer does.

None of the five declared an abstract member, so the modifier was carrying no
compile-time obligation either. It was redundant with the protected constructor for
the only purpose it served.

### Accepted change

`abstract` dropped from all five bases on both platforms. One line per type.

## Finding 2 — the surface bases scheduled work on a handle that did not exist

### Observed

Opening a prompt in the designer failed with:

> Invoke or BeginInvoke cannot be called on a control until the window handle has
> been created.

`DialogViewBase`, `PromptViewBase<TResult>` and `PopoverViewBase` (WinForms only —
the WPF bases have no equivalent path) subscribed `ParentChanged` in their
constructor and called `BeginInvoke` from the handler, to undo the host's
`Dock = Fill` after `IViewHost.AddView` completes. The designer parents the control
too, at a point where no window handle exists, and `BeginInvoke` throws rather than
queueing in that state.

All three already exposed a `DesignMode` property that accounts for
`LicenseManager.UsageMode`. None of them consulted it on this path.

`ToastViewBase` overrides `OnParentChanged` without deferring and was unaffected.

### Why this is not only a design-time defect

`BeginInvoke` throws on *any* handle-less control, not only inside a designer. The
host parents a surface before it is displayed, so the same call was a latent runtime
fault for any consumer that added a surface to a container that had not yet been
realized. The design surface is simply the most reliable way to reach that state.

### Accepted change

Two guards, in all three WinForms bases:

- `if (DesignMode) return;` at the top of the handler. The design surface must keep
  the layout the designer defined, so none of the fixup applies there anyway.
- The deferral goes through a `RunWhenHandleReady` helper that uses `BeginInvoke`
  when a handle exists and otherwise parks the action for `OnHandleCreated`.

## Verification

- `SurfaceBaseDesignTimeTests` — a theory over every public surface base found by
  reflection across both platform assemblies asserting none is abstract, plus a
  behavioural test that parents a dialog, a popover and a prompt into a handle-less
  host and asserts no throw. The theory's type filter compares the name **before the
  backtick**, because a generic type reflects as `PromptViewBase\`1`; an earlier
  draft matched the raw name and silently skipped both prompt bases — the exact types
  the test exists for.
- Navigation suite: 267 → 278 tests, passing on `net481` and `net9.0-windows`.
- Full solution: 22 test assemblies, zero failures.
- Designer, before and after: `ReasonPrompt` failed to load with the handle error at
  `f169ab6` and renders its full layout at `73ddbdb`.

## Residual gaps

### `PromptViewBase<TResult>` is still generic — deliberate, not scheduled

A generic base is the remaining shape the WinForms designer refuses. A consuming
prompt therefore still needs a non-generic shim closing the type argument — the
`FarmDatabase` scenario carries `ReasonPromptBase : PromptViewBase<string>` for
exactly this, and only this. Dialogs, toasts and popovers need no shim at all now.

Closing this means moving the result type off the view: `IPromptView` would complete
with `object` and `IPromptService.ShowPromptAsync<TPrompt, TResult>` would keep the
typed call site and cast. That removes the shim for every result type instead of one
per type. It is a **public contract change** and has not been accepted — it is
recorded here as a known cost, not as pending work.

### `[DesignerCategory("Code")]` is an unguarded trap

The attribute instructs Visual Studio to open the code editor rather than the design
surface. It is correct on custom-painted controls and wrong on any page or surface,
and nothing — not the compiler, not an analyzer, not the type system — reports the
mistake. The scenario's own pages carried it initially and opened as code for that
reason alone.

This is a consumer-side hazard rather than a module defect, so no module change is
proposed. It is documented in the scenario's README next to the code that hit it.

## Files

- `src/Navigation/NekoLib.Navigation.WinForms/Hosting/{DialogViewBase,PromptViewBase,PopoverViewBase,ToastViewBase,AutoDismissPopoverBase}.cs`
- `src/Navigation/NekoLib.Navigation.Wpf/Hosting/{DialogViewBase,PromptViewBase,PopoverViewBase,ToastViewBase,AutoDismissPopoverBase}.cs`
- `tests/NekoLib.Navigation.Tests/Unit/SurfaceBaseDesignTimeTests.cs`
- `runtime_tests/Data/FarmDatabase/` — the consumer that produced the findings

Commits: `f169ab6` (scenario), `73ddbdb` (fix and tests), `5418cb2` (scenario docs).
