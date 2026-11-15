# Synchronous/asynchronous hosting diagnostic

R387, 2026-11-15 UTC, unchanged **810379be / frozen 8fbfa3f0**. Existing
`CallOverheadBenchmarks.CallsIdentity10k` / `CallsIdentity10kAsync` execute the
same compiled 10,000-call identity script; constructor checks require the exact
integer 9,999. Each timed invocation checks success and disposes its result;
compiled scripts are disposed by benchmark cleanup. No runtime or harness change.

| Observation | Run mean | RunAsync mean | Within-process async overhead |
| --- | ---: | ---: | ---: |
| Initial process 1 | 1.022 ms | 1.433 ms | +40.23%; invalid as a stable estimate: sync IQR/median 48.20% |
| Initial process 2 | 1.266 ms | 1.433 ms | +13.19% |
| Longer warmup, process 1 | 1.296 ms | 1.403 ms | +8.25% |
| Longer warmup, process 2 | 1.274 ms | 1.351 ms | +6.00% |

Both longer-warmup processes retain every observation. Their async IQR/median is
4.17% / 6.02%; all cross-process mean pairings span +4.25–10.07%. Median pairings
are wider, +2.26–12.27%. The initial transition from roughly 0.69–0.71 ms to
1.27 ms sync observations remains visible; no causal tiering explanation is proved.
The confirmation reduces uncertainty without erasing that adverse control.

Existing BenchmarkDotNet in-process toolchain, two separate host processes per
group, ordinary runtime GC/tiering, no tool-forced GC and no outlier removal.
Initial: three 100 ms warmup iterations / five measurements. Confirmation:
twenty 500 ms warmup iterations / seven measurements. Total host durations are
15.026 s and 71.919 s respectively. Every file of the copied frozen image matches
before and after each group; no generated build can substitute a different core.

Local Windows 11, i7-14700KF, .NET 11 RC3. Methods run sequentially in their existing
order, not interleaved; scheduling, host state and process controls remain possible
contributors. These clocks include public invocation, result cleanup and API/task
completion. They exclude compilation, startup and genuinely pending host I/O.
MemoryDiagnoser's raw output is preserved but is not an allocation or heap claim:
its scope across execution-worker threads was not qualified here.

Decision: a modest API gap is plausible, but this diagnostic does not attribute a
dominant whole-script cost or justify another dispatcher. Use the existing worker's
`warm` / `warm-async` lanes on complete scripts before selecting a redesign. Preserve
actual suspension, cancellation, terminal admission refusal and joined ownership;
full qualification of this unchanged runtime is [already complete](../2026-11-15-function-slot-binding/delivery/README.md).
No portable speedup, CPython ratio or performance-objective completion is claimed.

[All statistics and original values](summary.json), [full logs, image identities,
exports and exact procedures](observations.tar.xz).
