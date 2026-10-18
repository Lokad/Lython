# Finalized compiler slot maps, 2026-10-18 UTC

Code objects take the two completed local/closure slot dictionaries directly
from their single-use compiler builder. Two dictionary clones and their copy
work disappear. Capture collection finishes first; all four builder entry
routes create a fresh builder and use it once. Runtime consumers only read the
published maps. The production patch replaces two expressions and adds a comment;
no public API, execution route, owner, budget, cache or freeze mechanism changes.

Baseline: `b683730fcbecfbe95213fa978842d8e7f388be72`; candidate: that revision
plus the [one-file patch](candidate.patch). One frozen pair records allocations
for ten source forms through native and public compilation and five pure warm
forms under both execution APIs. Two warmups, eight samples, eight calls/sample;
no capture retry or post-capture production tuning.

| Complete script source | Native compile bytes, before -> after | Public compile/dispose bytes, before -> after | Saved bytes | Extra retained cell payload |
| --- | ---: | ---: | ---: | ---: |
| CSV helper | 464,232 -> 463,248 | 466,072 -> 465,088 | 984 | 0 |
| Text helper | 374,928 -> 374,112 | 376,768 -> 375,952 | 816 | ~168 B |
| JSON helper | 433,984 -> 433,000 | 435,824 -> 434,840 | 984 | 0 |
| File-manifest helper | 521,432 -> 520,616 | 523,272 -> 522,456 | 816 | ~168 B |

Native compilation includes frontend/lowering/executable construction, excluding
the public admission/owner wrapper. Public compilation includes that wrapper,
diagnostic cache and script disposal under an already-created shared root/engine.
Neither compilation boundary executes a script. This is not compile-to-result.
Helpers/closures/lambdas/generators/genexprs and the empty control save 160-1,024 B.
All ten warm allocation/work arrays and code/slot/closure/capture/span fingerprints
match. Warm execution excludes compilation, context setup, startup workers,
projection and I/O. Pure async values complete on the measured thread. All
measured owners return allowance to zero; frames/depth/temporary reservations clear.

The handoff retains builder spare capacity instead of count-sized clones. Text
and file sources retain six additional dictionary cells: roughly 168 B of X64
entry/bucket payload, plus possible alignment differences. Their existing
compilation allowances are 73,472/87,872 B. Other observed forms retain no extra
cells. This is a modest documented retention tradeoff, not a heap/RSS reduction
or a proof that the allowance exactly describes CLR memory. Coarse source-sized
admission and host headroom remain; there is no new per-object accounting.
[MEMORY.md](../../../MEMORY.md) states the ownership contract.

All 418 distinct focused Release checks pass: 117 white-box, 149 public language/
capture/lifetime cases, 90 shared-budget/admission/cache cases and 62 complete-job/
cancellation cases (58 CSV/text/JSON/file jobs). Existing tests cover concurrent
executions sharing compiled storage, independent defaults, retained results,
transitive cells, lambdas/comprehensions, actual suspension/cancellation, refused
compilation before large frontend work and awaited cleanup. Five small semantic
goldens independently match CPython 3.13.2. No permanent architecture tests added;
temporary diagnostic removed and original Stress/assertions/timeouts preserved.

Local Windows 10.0.26200 X64, i7-14700KF/28 logical processors,68.4 GB;
SDK10.0.300-preview.0.26177.108/runtime10.0.13, ordinary test-process settings.
No elapsed measurement, latency/CPython gain, complete-job speedup or quiet-machine
claim. [Scope](measurement-scope.json), [source screen](source-screen.json),
[diagnostic](diagnostic.cs), [baseline](baseline.trx), [candidate](candidate.trx),
[comparison](comparison.json), [goldens](goldens.json) and [validation](validation.json)
preserve all evidence. Raw per-map counts/capacities remain in both TRX files.
Package/platform delivery qualification is recorded separately in PLAN.md.
Stop at these two map handoffs; regex remains planning only.
