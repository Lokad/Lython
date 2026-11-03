# Standalone allowance gate experiment — B177

2026-11-03. **Withdraw the runtime primitive swap**, retaining the independently
useful concurrent-resize regression. No current runtime contract or API change.
The initial named monitor share was 5.64%text/10.62%report; it did not predict a gain
from changing the framework primitive. Uncertainty is not a permanent design ban.

Changed only the private LythonMemoryBudget state gate from object to the fully
managed framework System.Threading.Lock. All critical sections, shared limits,
refusal semantics and owner lifetimes unchanged. No new package, headroom tier,
pool, atomics fast lane, quota or configuration. Both actual targets build with zero
warnings; **164 focused public checks per .NET 11/.NET 10 PASS**, including shared
executions, retained results, actual callback cancellation/refusal, scope disposal
and reuse. Extend the existing 8-worker×128 admission control to grow/shrink each
admitted reservation while other owners execute and finish; preserve that test on
the original gate. No larger stress scale or relaxed bound.

## Complete jobs and direct diagnostic

Five complete RAM-file CSV/text/JSON/report/config jobs, BOTH APIs, 256 warmups,
nine rotated 64-job samples and two-second settle/case. Before/new/identical-old
control in isolated cores, common CoreLib/hash/process GC. Actual I/O, reset, full
stdout/file checks and result disposal included; startup/compilation/final cached
teardown excluded and joined afterward. Quiet Azure Standard_F4as_v7, Ubuntu24.04.4,
.NET11RC3, ordinary GC/tiering. All goldens, identities, balances, gates, fixture
removal and joins PASS; owned **148.475 s**, exit zero. Complete-job raw snapshots
were retrieved and SHA-256 verified locally.

| Case | Sync elapsed / unchanged control | Async elapsed / unchanged control |
| --- | --- | --- |
| CSV |+2.81% /+8.30%|+1.93% /+0.53%|
| Text |−6.78% /+1.41%|−0.57% /+5.19%|
| JSON-lines |+0.68% /+0.31%|+0.77% /+0.88%|
| Report |+2.65% /−0.98%|−0.84% /+0.03%|
| Config |+0.32% /+0.34%|+0.90% /−0.04%|

Allocation/peaks essentially flat. Large identical-control movement makes broad
claims unsafe; no particular raw elapsed delta is automatically an attributed
primitive gain or loss.

One additional **short direct public resize diagnostic** tests the actual mechanism,
not another full-script retime or benchmark framework. Same frozen cores, three
isolated images, bound delegates outside clocks, balanced warmup, two-second settle,
nine rotated samples of 1,048,576 grow128/shrink64 pairs. No reflection/allocation
in the timed loop. A separately capped 256 B allowance checks final size and returns
to zero after reservation disposal. No guest instruction/fuel counters. Owned
**4.189 s**, exit zero. Samples, identities and zero balances retained; service
asserted both quiet gates, but their raw snapshots were lost afterward (see below).

| Image | ns per grow/shrink pair | Elapsed IQR /median | Timed allocation |
| --- | ---: | ---: | ---: |
| Old object gate |26.977|0.134%|0 B|
| New managed Lock |26.101|1.892%|0 B|
| Identical old control |28.303|2.074%|0 B|

Candidate −3.25% versus old, while identical old controls differ +4.92%. This tiny
uncontended primitive delta does not establish a useful whole-script outcome. No
Lython mechanism was removed by the one-field swap. Withdraw, without a fixed veto
or permanent rejection; no extra full-script confirmation selected. Existing
qualified gate retained. Shared-host contention/latency and custom host-budget
implementations were not benchmarked by this diagnostic.

## Evidence storage limitation

After retrieving the resize samples, creating their full archive failed with
FileNotFoundError: the owned `/dev/shm` staging root was already absent. The raw
micro machine snapshots are unavailable; do not substitute earlier snapshots or
repeat unchanged timing to hide the loss. Its script and successful service log
preserve the executed gate checks. Main complete-job evidence remains intact.

VM settings show the default RemoveIPC enabled and lingering disabled. A paired
RAM/home control survived 60.87 s while other login sessions remained, so this
probe does NOT prove what removed the earlier staging. The cause remains unmeasured.
[Settings](storage-settings.json), [probe creation](storage-probe-created.json),
[probe observation](storage-probe-observed.json). Future catalogs, workers, raw
snapshots and logs go in persistent working storage, separately from RAM fixtures.
Large RAM traces must be copied/verified before collection ends. No VM policy changed.

## Identities and restoration

Before: fully qualified runtime source **7c183083**, test correction **7e96843b**,
CI41693945867, frozen core
**bf2ddeb1d6adb8b4dccda00e0980225502b3a5a92e8aec019ab753ac3a6e69ed**.
Prototype **6bf29cb93985725f7a367264f1e1b23d5f6e970717d7aa7feebe24c84fca4f3f**.
Exact source/test hashes, prototype snapshot and adverse observations retained.
No CPython/compilation refresh or release package generated for this experiment.

Original production gate restored and nonincrementally rebuilt; **164 public
checks per actual .NET 11/.NET 10 PASS**. Only the useful concurrency-test extension
remains. No new runtime option/backend or dormant candidate, no repeated full
qualification for restored production sources. B174/B171 accepted timing debt
remains explicitly recorded, not qualified away by this experiment.

[All initial tables](comparison-tables.md), [assessment](assessment.json),
[initial raw observations](observations.tgz), [resize assessment](micro/assessment.json),
[resize samples](micro/measurements.json), [service log](micro/vm.log),
[identity](identity.json), [source snapshot](source-snapshot.tgz),
[CPU motivation](../2026-11-03-current-execution-cpu/README.md).

Restored-source checks finished before freezing another candidate. The final owned
RAM root was already absent when cleanup checked it; lease independently free.
Future evidence collection uses persistent storage. No VM process or discarded
primitive remains active; original source is unchanged apart from retained tests.
