# Current captured-text CPU attribution — R292 / B647

2026-11-13; fully qualified source **2d91eb1a**, frozen core SHA256
`161cd935023fd95a08488cbf7cfa3ee02ea4b88c69c0ba1231299b6ef0d80447`.
Same fixed fec79 benchmark worker, adapter0.4, stock .NET11RC3 on the quiet
F4as_v7 / Ubuntu24.04.4 VM. The complete captured-helper/nonlocal text script
reads 3,072 lines /35,840B and validates its full stdout and files.

Twelve-second warmup; eight-second CPU-clock capture at199Hz with frame pointers.
Selected guest thread consumed7.64 CPU seconds; execution clocks total7.776941s.
**1,518 samples**,97 unmapped leaves(6.39%),0 ambiguous leaves,0 malformed CLR map
lines and0 reported lost samples. Normal GC/tiering; PerfMap is the only diagnostic
runtime override. CLR symbols are resolved against the saved IP intervals;
overlaps are not guessed. Full all-thread CPU and native/managed raw stacks survive.
This is CPU attribution, not a wall-time, allocation or before/after comparison.

| Selected leaf (including inlined useful work) | Samples / share |
| --- | --- |
| TryExecuteDictionaryLoop | 264 / 17.39% |
| PrepareCasedWhitespaceIteration | 138 / 9.09% |
| ExecuteInstructionPrefix | 118 / 7.77% |
| Unmapped CLR leaf | 93 / 6.13% |
| RunCore | 77 / 5.07% |
| Exit | 53 / 3.49% |
| Enter | 42 / 2.77% |
| WriteCaseUtf8 | 42 / 2.77% |
| TryStartNativeCall | 38 / 2.50% |
| RetireTo | 34 / 2.24% |
| Reserve | 34 / 2.24% |
| ExecuteForNext | 34 / 2.24% |
| TryExecuteNativeCall | 32 / 2.11% |
| MapCaseRune | 30 / 1.98% |
| Call | 29 / 1.91% |

Nearest Lython frame attributes8.23% to MemoryGovernor.Reserve and3.82% to
EnsureInterpreterStack; those include runtime/monitor work below their callers.
They are not independently additive to the leaf rows or removable shares.

## Source gate and next decision

This repeats the broad cost distribution of R272 on the **new** ordered-slot
implementation. It does not show a remaining duplicate argument array or prove
that universal tagged values, a new call controller, or frontend passes dominate.
The dictionary loop does useful probing, exact arithmetic and Python binding;
the previous segment-entry/frame-hoisting variants already measured slower and
remain withdrawn. Existing case writing is UTF-8/bulk ASCII, not per-character
CLR string conversion. Do not infer a new casing-conversion project from the name.

The next source gate is **per-record text construction and admission**: cased
iteration currently collects ranges into stack/growing scratch, rents its final
range backing, copies the inventory, then admits eventual word storage. Assess
whether one directly owned construction buffer can replace that staging/copy
across split consumers without creating another pool or per-object ledger.
This is a concrete elimination hypothesis, not a speed forecast from the9.09%
preparation leaf. Admission is an observable callback: preserve allocation peaks,
first refusal before loop effects, mutation after reentry, final-sigma/expansions,
retained strings, early exit and pending async cleanup. Native/guarded search and
CSV are controls; captured/native text are affected scripts. First stop is a
source/ownership design plus existing regressions showing whether staging is
removable. A prototype then needs compile/warm/compile-to-result controls and a
delivery debt decision. If the common replacement cannot simplify ownership,
record that result and proceed to the call/frame roadmap; do not add a second
construction framework merely to remove a copy.

No new runtime prototype is retained by this capture. The performance objective
remains open; accepted native-search debt and uncertain guarded timings remain
in the ordered-slot evidence. Compilation/front-end consolidation remains a
later whole-pipeline milestone, with compile-to-result controls required.

## Failure preservation and custody

The first private collector substituted a placeholder inside DOTNET_ROOT. Its AST
gate stopped before RAM mounting or worker start; corrected source is retained.
The valid capture completed, verified goldens/balances and quiet gates, joined all
users and unmounted fixtures. The service then exited1 because root-owned
perf.data could not be archived. Under a fresh idle lease, only that verified
regular task-owned file had ownership corrected; inode and bytes were unchanged.
The complete archive was rebuilt and independently SHA-verified, without repeating
samples. The original partial archive and journal are preserved. Report the
original failed service condition separately from successful evidence recovery.
Last owned VM free space265224192B; no active users or RAM fixture mounts.

[Attribution](attribution.json), [raw recovered capture](observations.tgz),
[original partial archive](observations-first.tgz), [recovery](archive-receipt.json),
[cached terminal state](terminal-status.json),
[collection and analysis sources](collection-sources.tar.xz).
