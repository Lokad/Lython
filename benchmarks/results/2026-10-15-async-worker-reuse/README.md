# Reusable asynchronous startup workers

Both public execution APIs now use the existing protected worker pool. Async
scheduling returns without waiting through a CPU prefix. After a prefix returns
an incomplete task, its worker becomes available for another execution; no worker
waits through host I/O. This removes the separate per-run thread construction and
async-void relay. Each request enters/restores its own CLR execution context;
completion removes result, runner, failure and context roots from idle work items.

The compatibility tradeoff is bounded idle retention: at most eight workers (or
the processor count, if smaller), retiring after 30 seconds, each reserving a
16 MiB stack. Busy prefixes do not queue other executions. This changes startup
resource reuse, not managed nesting protection or invocation ownership. Suspended
runs keep their funded state until completion, cancellation and cleanup.

| Already compiled `pass`, warm `RunAsync` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Invocation setup control, no input | 110.505 us | 14.339 us | 1.661 us |

One [prototype diagnostic](warm-async.json) observed a substantially lower setup median.
Baseline/candidate IQR-to-median is 17.1%/21.8%, CPython 0.6%; retain this noise.
This is a setup control, **not a useful-script speedup, qualified multiplier or
tail-latency result**. This predates the structural ambient correctness fix below;
it does not measure the final delivered source. No repeat, retuning or replacement samples. The owned group
took 7.589 seconds including cleanup, under a 30-second bound. Seven rotating
batches, calibration and warmup remain in the receipt.

The timer includes invocation initialization, public execution, result
creation/projection, cheap output checks and prior-result cleanup. Compilation,
process startup, transport and full response hashing are excluded. CPython uses
synchronous execution; there is no host I/O in this control. Genuine suspension,
context isolation and cancellation are correctness checks, not injected-delay
performance comparisons. This does not measure fresh compile-to-result.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 using
ordinary GC/GIL, `-I -S`, UTF-8 mode off and explicit UTF-8 protocol I/O. Lython's
normal public policies and GC/tiering remain; CPython has no equivalent containment.

Baseline is 05eb4efcda4d613d2026e8b8ef0d570034a12c7c. The working candidate adds
only two performance files, frozen by [hashes](comparison-declaration.json).
The benchmark catalog, execution tooling and Python worker are unchanged.
[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match the independent empty-output golden. [Cleanup/source proof](cleanup-source-proof.json)
verifies seven worker processes exited, exact production differences, identical
tooling, inactive services and lease availability.

Focused checks cover nonblocking scheduling, reuse across sync/async callers,
worker reuse while another execution remains suspended, original exceptions and
cancellation status, context restoration, idle expiry/disposal, reentrant host
callbacks and managed stack guards. Eight genuinely pending host operations reuse
one startup worker, retain distinct contexts, and finish or cancel correctly.
Quiet GC regressions check idle workers retain no host, script, result, exception
payload or caller context. Existing governed streaming/lifetime checks remain.

The complete public run then exposed stale cancellation in an uncancelled
retained-text workload. Async dictionary/set construction, comprehensions and
iterable constructors held thread-local structural ambient scopes across awaits.
Those scopes now cover only synchronous insertions/comparisons after evaluation
and iterator pulls. Ten genuinely yielding cases check host callbacks see no
structural ambient; retained-text denial and owner cleanup remain required.
The final change adds five production files beyond the two measured files.
There is no second diagnostic or final-source timing claim.

[Validation](validation.json) records the build and test receipts, including a
fixed xUnit analyzer error, the initial full public run's two obsolete
thread-exit expectations, and the next run's stale-cancellation failure.
Thread-exit expectations are replaced by reuse assertions, preserving
pending-operation, cancellation and context checks. The preceding strip delivery's
CI also exposed a legacy keyword-acceptance fixture; it now passes positional
chars while explicit keyword-rejection regressions remain. No limits, timeouts
or containment assertions were relaxed. Full stress coverage is preserved; it
was not rerun in this slice. The qualified ordinary suites pass 2,212 white-box
and 8,889 public tests (14.124 and 65.694 seconds including cleanup). Focused
groups overlap: 41 white-box and 26 public cases after the ambient fix, both
under four seconds. Package/platform qualification follows at delivery.

Immutable metadata sharing, larger frame layouts and other AP00–AP07 work remain
open. This closes the async startup worker migration only.
