# Testing Lython

During development, build the affected project once and run its focused Release
regressions. For example, from the repository root:

```powershell
dotnet build tests/Lokad.Lython.PublicApi.Tests -c Release
dotnet test tests/Lokad.Lython.PublicApi.Tests -c Release --no-build --filter 'Category!=Stress&FullyQualifiedName~InstanceLifetimeScenarioTests'
```

Use the white-box project for internal storage, binding and governor changes.
Keep allocation preflights, exact retained-data rejection, escaped aliases,
cancellation and successful bounded streaming in the selected regression group.
Build `tools/LythonProbe/LythonProbe.csproj` in the matching configuration before
running tests that invoke the probe; a test-project build does not build it.

Ordinary `dotnet test Lokad.Lython.slnx -c Release` runs all regressions except
`Category=Stress`. `Directory.Build.targets` supplies that default only when no
filter is provided. An explicit `--filter` replaces the default: include
`Category!=Stress` when selecting broadly by name. The small lifetime fixtures
use fixed caps and enough iterations to exhaust those caps if charges strand;
they do not depend on loose elapsed-time or peak-ratio assertions. Retention
fixtures also check that a single integer result fits the same budget.

At a coherent containment or dependency checkpoint, batch the complete checks:

```powershell
dotnet build Lokad.Lython.slnx -c Release
dotnet test Lokad.Lython.slnx -c Release --no-build
dotnet test Lokad.Lython.slnx -c Release --no-build --filter 'Category=Stress'
./tools/PackageSmokeTest/verify-package.ps1
```

The stress selection preserves the original long lifetime/counting workloads,
large parsed integers and factorials, CSV scales and unpacking peak comparisons.
Most share their bodies and assertions with the small tests through derived
fixtures; the derived classes restore the original counts and caps. Full
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
