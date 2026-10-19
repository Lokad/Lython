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

Governed lifetime controls use finite host batches under one shared root. Check
that options actually borrow that root, then verify
accounting returns to the compiled baseline after each result is relinquished,
and return to zero after compilation cleanup. Keep fixed-cap retained-data and
before-allocation refusal witnesses beside those success controls. A guest
deletion, cleared container or CLR collection does not promise an immediate
refund; see [MEMORY.md](MEMORY.md). Remove tests enforcing the discarded collector
architecture while preserving Python results, aliases, cursor state, rollback,
physical reachability and cancellation. Do not replace these with loose timing
or peak-ratio assertions. Refused memory admission must fail the command even
through guest catches, context-manager suppression and finally returns. Preserve
before-mutation, cursor and native-cleanup witnesses; replace successful
same-command budget-retry expectations with failure plus a separate funded command.
Keep ordinary guest-raised MemoryError catchability and retained-owner checks.

At a coherent containment or dependency checkpoint, batch the complete checks:

```powershell
dotnet build Lokad.Lython.slnx -c Release
dotnet test Lokad.Lython.slnx -c Release --no-build
dotnet test Lokad.Lython.slnx -c Release --no-build --filter 'Category=Stress'
./tools/PackageSmokeTest/verify-package.ps1
```

The retained set/`None` contrast uses 512 cells under 32 KiB during ordinary
development. Stress keeps the original 100,000-cell growth loops: the set refusal
cap remains 3 MiB, while the successful `None` control gets 4 MiB for its measured
3,540,064-byte replacement peak. ZIP open-member cancellation waits for a host
checkpoint inside the write loop before cancelling; ordinary coverage writes
one byte, and Stress retains one million writes. These fixtures preserve refusal,
cleanup and cancellation assertions without depending on a scheduling delay.

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
Release regressions and the explicit Release package consumer. CI completes the
solution build, including the probe, before tests with `--no-build`, so solution
compilation does not overlap suspension/cancellation checks. This sequencing
removes that source of contention without changing assertions or deadlines;
passing it does not establish the cause of an earlier timeout. Dispatch the same
`ci` workflow with `stress=true` to include the original stress scales on both
platforms. Keep existing cancellation and hang limits; do not extend timeouts to
hide interference. CI status checks should remain minutes apart.

Tests that explicitly collect or wait for finalizers share the existing
nonparallel `AllocationSensitive` (white-box) or `RuntimeStress` (public)
collection. Process-wide GC counters and weak-reference observations otherwise
change when another fixture collects. Ordinary semantic tests remain parallel.
This isolation applies within each test process; it does not establish host
latency or eliminate machine-wide CPU contention.

The regex boundary uses a cooperating provider for in-flight interruption and
funded cleanup. Adapter integration runs separately and reports unsupported
cancellable backend work explicitly. The package gate restores both a core-only
consumer with no regex dependency and an optional adapter consumer. Old tests of
value-observation hooks, scalar coupons and independent local-cap diagnostics are
removed; capacity estimates and construction overlap can reject earlier. Native
publication copies remain charged until their owner ends. Retained scalar-only
results need not pin an execution that has no surviving storage.
