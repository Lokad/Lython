# Lists admitted by retained capacity

`423967d2` consolidates list backing and possible numeric-box headroom into one
capacity estimate. It deletes per-value numeric scans/subtotals, repeated numeric
admission/refunds and their rollback deltas: 140 net production lines removed.
In-capacity mutation retains funding. Growth admits full replacement before copy
and relinquishes only old backing afterwards; unpublished failures refund only
private storage. Existing producer funding, first owners and independent large
numeric payload ownership remain. See [MEMORY.md](../../../MEMORY.md) for the
intentional earlier-refusal and internal-attachment tradeoffs.

185 focused white-box checks, 61 public ownership/provider checks, 59 public
protocol/cancellation/preflight checks and 123 complete CSV/text/JSON/file-script
checks pass in Release. Representative host batches share a finite 512 KiB root
and return to their compiled baseline. Refusal fixtures now exhaust backing
capacity rather than requiring the deleted per-value admission stage.

One 7.827-second capture on 2026-10-19 uses the maintained nested-comprehension
helper kernel at size 1024. Every engine produces exactly `1364 699050`.

| Engine | Fresh compile and complete kernel | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `118ce764` | 0.995 ms | 38.55% | Before; diagnostic |
| Lython `423967d2` | 0.920 ms | 9.42% | Capacity consolidation; diagnostic |
| CPython 3.13.2 | 0.133 ms | 10.70% | External context on this machine only |

The 7.56% lower candidate median is small relative to baseline spread. No speedup,
regression, allocation reduction or embedding-latency claim is established.
The source simplification stands independently of inconclusive timing. Stop after
this capture; no retiming or broader ownership/measurement campaign follows.

Each invocation compiles afresh in an already-running host, including output
checks and compilation/result cleanup. Startup and worker protocol are excluded;
compilation alone and warm execution were not separated. Seven alternating rounds
follow warmup on shared workstation LOKAD-0399, x64 Windows build 26200,
28 logical processors, .NET 10.0.13, workstation GC. The budget is explicitly
unbounded for timing; finite-budget behavior is tested separately. Exact outputs
match before and after timing. The owned parent/workers exited within the bound.

Evidence: [source patch](candidate.patch), [raw samples and identities](micro.json),
[process receipt](process.receipt.json), [baseline identity](baseline.json),
[focused check receipts](checks.json), [scope and hashes](scope.json). The full
unchanged input catalog is shared with [the preceding diagnostic](../2026-10-19-native-prepared-routing/catalog.json.gz);
its decompressed hash matches the receipt, which contains the selected source,
fixture and independent golden. Existing comparison tooling was reused.

The diagnostic above predates the extension correction in `fd5b60ab` and fixture
migration in `bb38b2f0`; it is not a measurement of those revisions or current HEAD.
The correction consolidates extension loops, checks cancellation during known-size
stores, funds actual growth when a reported count changes, and removes redundant
periodic counters. All three tiny controls reproduce failures on the preceding
implementation and pass after the correction. [Patch](extension.patch) and
[follow-up qualification receipts](extension-qualification.json) preserve both.

Initial full CI 39327618805 failed on stale success/fee fixtures and one Windows
float-conversion wait. Success controls are reduced while their original large
DAG/CSV/scalar cases remain in Stress with explicit capacity headroom; refusal caps
and existing Stress counts are preserved. Two touched batch fixtures now borrow
their supplied root. Eight selected large-scale controls pass locally; the six
float-conversion cases pass unchanged in focused runs, with no deadline changes.
Both explicit Release package consumers pass at `bb38b2f0`. Final cross-platform
ordinary/Stress/package qualification is pending; no full green is claimed here.
