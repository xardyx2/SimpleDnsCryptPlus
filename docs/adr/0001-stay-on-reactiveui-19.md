# ADR 0001: Stay on ReactiveUI.WPF 19.5.1, keep the `windows10.0.19041` platform floor

Date: 2026-09-27
Status: Accepted

## Context

The app pulls in two overlapping reactive libraries:

- `ReactiveUI.WPF` — used for exactly one symbol, `ReactiveCommand` (18 call sites, 3 files)
- `ReactiveProperty` (`Reactive.Bindings`) — used for `ReactiveProperty<T>`, its validation
  extensions (`SetValidateNotifyError`), and `ReactiveCommand` too, which is why `MainViewModel.cs`
  and `AddCustomResolverViewModel.cs` already carry
  `using ReactiveCommand = ReactiveUI.ReactiveCommand;`

The original plan said to bump `ReactiveUI.WPF` 19.5.1 → 24.3.0 in Stage 2, for one reason: 19.5.1's
newest asset is `net7.0-windows10.0.19041`, so a project targeting plain `net10.0-windows` (platform
floor *windows 7.0*) cannot use it and NuGet silently falls back to the **.NET Framework 4.8**
build — reported as `NU1701`. Running a net48 ReactiveUI inside .NET 10 is not acceptable.

## Decision

**Do not upgrade ReactiveUI.WPF.** Keep 19.5.1 and keep the project's TFM at
`net10.0-windows10.0.19041`.

## Rationale

The platform floor solves the actual problem. With `net10.0-windows10.0.19041`, the
`net7.0-windows10.0.19041` asset *is* eligible (same platform version, higher TFM), and
`dotnet restore` reports **0 `NU1701`** — measured, not assumed. This is also the exact
configuration instant.sc shipped as a signed-off 0.8.2 release with 11k downloads, so it is
proven in the field.

The upgrade itself was attempted and stopped on evidence, not preference. ReactiveUI 24 replaced
`System.Reactive.Unit` with its own `ReactiveUI.Primitives.RxVoid` / `RxUnit` in the factory
return types:

```
error CS0029: Cannot implicitly convert type
  'ReactiveUI.ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid>'
to 'ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>'
```

That is a redesign of the type the app uses most from this library, and it cascaded: adopting v24
also made `ReactiveUI.ReactiveProperty<T>` collide with `Reactive.Bindings.ReactiveProperty<T>`
(`error CS0104`), requiring per-site qualification. Roughly 18 command sites plus the property
collisions, in a library used for one type, for **zero behavioural gain** — no bug we have is
fixed by it, and no feature needs it.

## Consequences

- The app declares a minimum of Windows 10 19041 (2021). That is honest for a current
  dnscrypt-proxy deployment and matches what already shipped.
- Plain `net10.0-windows` stays unavailable while 19.5.1 is pinned. Revisit if a
  ReactiveUI-specific defect ever forces the move; then budget the `Unit` → `RxVoid`/`RxUnit`
  rewrite as its own change with a manual UI pass, not as a version bump inside another PR.
- A future dependency cleanup should ask whether `ReactiveUI.WPF` is needed at all: it is
  imported for `ReactiveCommand` alone, and `Reactive.Bindings` already provides a
  `ReactiveCommand`. Dropping it would remove the ambiguity class entirely rather than aliasing
  around it. Not done here — that is a behavioural change to 18 command sites, out of scope for a
  version-bump stage.

## Verification at time of decision

| Check | Result |
|---|---|
| `dotnet restore` with 19.5.1 on `net10.0-windows10.0.19041` | 0 `NU1701` |
| `dotnet build -c Release --no-incremental` | succeeded, 0 errors, warning set identical to Stage 0 baseline |
| `dotnet test Tests -c Release` | 5 passed, 0 failed on net10.0 |
