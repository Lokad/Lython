# Synchronous dispatch improvement, 2026-10-09

Separating synchronous instruction dispatch from the async state machine reduced
the basic loop's median time by **13.4–14.0%** in two short diagnostic comparisons.
Both collections took about **7 seconds**, with a 30-second external deadline
including owned-worker cleanup. Full comparison lanes are reserved for milestones.
These observations have no milestone qualification or claimed confidence interval.

The measured candidate is clean Release source
`891798a6ffa9a35494b6bd32e86ba0038d2b3425`; its reference is
`56d4346d960b58f037da7d4590eb66c87ffec29e`, the source used in the
[earlier basic-loop investigation](../2026-10-09-core-loop/README.md).
The timed Lython worker and CPython helper implementations are unchanged between
those revisions. Each engine reuses the same precompiled module, creates fresh
invocation state and checks the complete scalar output against the same golden:

```python
N = 16384
total = 0
for i in range(N):
    total += i
print(total)
```

The comparison includes ordinary public invocation costs and Lython's governor.
The pinned VM uses .NET 10.0.12/SDK 10.0.401 and optimized CPython 3.13.16,
with normal tiering, GC, public limits and no optional instruction fuel.
Builds and tests completed before collection. The shared VM lease excluded other
benchmark jobs; no builds, transfers or diagnostic instrumentation ran during
timing. This short workflow does not perform the milestone idle/noise gates.

| Collection | Baseline Lython | Candidate Lython | CPython context | Median time reduction |
| --- | ---: | ---: | ---: | ---: |
| 1 | 3,783.613 µs | 3,253.771 µs | 606.617 µs | 14.0% |
| 2 | 3,826.552 µs | 3,312.210 µs | 582.364 µs | 13.4% |

Each collection warmed each worker for at least one second, allowed two seconds
for ordinary background compilation to settle, then collected seven batches per
engine in rotating/reversed order. Batches were calibrated to at least 25 ms.
IQR/median was **3.8% / 3.6% / 1.6%** for baseline/candidate/CPython in collection 1,
and **3.4% / 5.0% / 2.9%** in collection 2. Every invocation, before/after
verification and loaded-binary/input-hash check succeeded; workers closed and both
services exited successfully. Collection is diagnostic, without selecting retries.

The new [synchronous interpreter](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.Interpreter.Sync.cs)
uses the existing opcode handlers and per-instruction execution checks.
[Abrupt routing](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.Interpreter.Routing.cs)
is shared with the async interpreter to preserve return, exception and cleanup
behavior. Prepared operations retain their existing shared implementation.
Simple-name loop assignment still uses its async helper; this first change isolates
dispatch so a subsequent assignment experiment can be measured independently.
Integer representation, arithmetic protocols, memory accounting and invocation
thread containment are unchanged. CPython remains substantially faster here.

Correctness validation passed **8,893 full Release tests**, **2,926 focused Debug
tests** covering control flow, generators, exceptions, functions, execution and
hosts, and **7 Linux Release microbench harness tests**. The broader comparison
harness also passed 354 local Release checks. No new performance threshold is a
unit-test acceptance criterion.

Raw receipts and preparation/test evidence are retained in ignored task storage
under `.git/agent-notes/sync-dispatch-20261009/`. The receipt clock spans were
13:37:20.5126821–13:37:27.6748366 UTC and
13:37:52.1398892–13:37:59.2794050 UTC. The systemd services impose the independent
30-second whole-process-group cap. Before/after files, worker identities, all
warmup/calibration/verification responses and all 21 measurement batches are
preserved in each receipt.

| Artifact | SHA-256 |
| --- | --- |
| Collection 1 receipt | `766270de9b34e0f2d9a8c5e35af0638f5ff88ed1d7b9db1d56e7e1bbc0fede37` |
| Collection 2 receipt | `bb47abc7736efba0fc71c251a4b00465c8b20600aee968a7da8c0a125576b593` |
| Shared core-loop catalog | `6f2a5f018909e56a1d7fc62cceec75f4803687a0413c86f7a17ffc9727cd2092` |
| Baseline production library | `102c2c99ff54e52d595e18180a915aa12dd9fabc61ddbc7ce76f36289092a867` |
| Candidate production library | `27b08fc013541e41c3975d02f4b93e49055df9ac5bf1918947055edfb5d62624` |
