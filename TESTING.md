# Testing Lython

During development, build the affected project once and run its focused Release
regressions. For example, from the repository root:

```powershell
dotnet build tests/Lokad.Lython.PublicApi.Tests -c Release
dotnet test tests/Lokad.Lython.PublicApi.Tests -c Release --no-build --filter 'Category!=Stress&FullyQualifiedName~Lokad.Lython.PublicApi.Tests.Governed'
```

Use the white-box project for internal storage, binding and governor changes.
Keep allocation preflights, retained-data rejection, escaped aliases,
cancellation and successful bounded streaming in the selected regression group.
Build `tools/LythonProbe/LythonProbe.csproj` in the matching configuration before
running tests that invoke the probe; a test-project build does not build it.

Ordinary `dotnet test Lokad.Lython.slnx -c Release` runs all regressions except
`Category=Stress`. `Directory.Build.targets` supplies that default only when no
filter is provided. An explicit `--filter` replaces the default: include
`Category!=Stress` when selecting broadly by name. Use a class-qualified filter;
`FullyQualifiedName~Governed` also selects unrelated legacy methods containing
that word.

Governed lifetime controls use finite host batches under one shared root, verify
accounting returns to the compiled baseline after each result is relinquished,
and return to zero after compilation cleanup. Keep fixed-cap retained-data and
before-allocation refusal witnesses beside those success controls. A guest
deletion, cleared container or CLR collection does not promise an immediate
refund; see [MEMORY.md](MEMORY.md). Remove tests enforcing the discarded collector
architecture while preserving Python results, aliases, cursor state, rollback,
physical reachability and cancellation. Do not replace these with loose timing
or peak-ratio assertions.

At a coherent containment or dependency checkpoint, batch the complete checks:

```powershell
dotnet build Lokad.Lython.slnx -c Release
dotnet test Lokad.Lython.slnx -c Release --no-build
dotnet test Lokad.Lython.slnx -c Release --no-build --filter 'Category=Stress'
./tools/PackageSmokeTest/verify-package.ps1
```

The stress selection preserves longer lifetime/counting workloads, large parsed
integers and factorials, CSV totals and sustained unpacking batches. Sustained CSV
totals use explicit finite executions. Imported-code headroom covers the coarse
compilation estimate rather than the discarded deferred-node inventory. Many
fixtures share their semantic assertions with the small tests through derived
classes; collector recovery and identical tiny thresholds are not requirements.
Full
qualification can also be selected in one invocation with
`--filter 'FullyQualifiedName~Lokad.Lython'`. Stress cases are evidence of
sustained correctness, not performance measurements or embedding-latency proofs.

Push a reviewed batch at that checkpoint. Windows and Ubuntu CI run all ordinary
Release regressions and the explicit Release package consumer. Dispatch the same
`ci` workflow with `stress=true` to include the original stress scales on both
platforms. Keep existing cancellation and hang limits; do not extend timeouts to
hide interference. CI status checks should remain minutes apart.

Tests that explicitly collect or wait for finalizers share the existing
nonparallel `AllocationSensitive` (white-box) or `RuntimeStress` (public)
collection. Process-wide GC counters and weak-reference observations otherwise
change when another fixture collects. Ordinary semantic tests remain parallel.
This isolation applies within each test process; it does not establish host
latency or eliminate machine-wide CPU contention.
