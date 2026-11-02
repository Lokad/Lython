# Shared owned iteration — B127

2026-11-02; baseline90785737/core137b563a. Final source patch/snapshots and exact
loaded hashes retained; final corea3a53e2e6e29239b3683a59eb78acadb969a1d78246f8cefd6f29d4d87c2ff02.
Retain this construction/lifetime simplification with explicit range regression
debt. It does not complete the substantial performance objective or refresh CPython
ratios. Publication remains postponed; regex integration is independent.

Synchronous executable loops and checked enumerable consumers now use the Cursor
already used asynchronously. CheckedEnumerator, CheckedRange, range.IterateChecked
and PeekIterator are gone:58 net production lines removed. Checked collection/source
shape remains for bulk capacity admission and self-extension. Native enumerable
validation no longer constructs then discards its sequence. Synchronous iterator
dispatch resolves once into the existing slot; PyUserIterator advertises its
existing async behavior through IPyAsyncIteratorValue, replacing a separate branch.
No new iterator framework, pool, owner, configuration or public API.

Disposal is terminal and clears roots after actual cleanup. Original subject,
lazy sequence and active state remain alive through mixed sync/async cleanup,
including failures. Breaking iteration does not close a retained Python iterator.
Dictionary version capture, lazy native acquisition, Python protocol acquisition,
live list mutations and async precedence are preserved. Existing admission,
cancellation, generator cleanup and stack protection remain in effect.

## Complete scripts and decision

Quiet Azure F4as_v7, Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Existing workers,
independent baseline/candidate/unchanged-control processes; four-second warmup per
image, nine interleaved ~200ms batches, two blocks. Full stdout/files, hashes, quiet
gates and joined fixture/worker cleanup pass. Existing range control is pure code;
file cases include their ordinary staging/reset and efficient mediated I/O.

| Final warm case | Elapsed change, blocks0/1 | Process CPU | Unchanged elapsed / CPU controls |
| --- | ---: | ---: | --- |
| Text/helpers |−2.21% /−2.33%|−2.71% /−5.92%|+2.35%/+4.92%; +0.06%/−3.45%|
| Search/early exit |−0.75% /−0.13%|−7.53% /+0.40%|−1.15%/−10.15%; +0.76%/+2.95%|
| CSV/helpers |+0.02% /−0.91%|−0.66% /−0.64%|−1.28%/+0.66%; −0.14%/−1.60%|
| Integer range loop |+2.02% /+2.80%|+2.02% /0.00%|−0.09%/+1.08%; +0.06%/−2.66%|

Text beats both unchanged controls by about2.4–4.5% elapsed,2.6–7.3% process CPU;
CPU controls drift, so magnitude remains diagnostic. Search is inconclusive;
CSV mixed/flat, not a general speedup. Range elapsed is about2–3% above controls
and remains accepted debt for the next coherent operand-transport milestone.
Retain that adverse result. Removing duplicate iteration state and the real
terminal-disposal fix are distinct from throughput improvement.

Initial b7e66c78 source: text elapsed+2.06%/−4.47%, controls+2.21%/−2.94%; search
+3.97%/−1.32%, controls+3.69%/−1.05%; CSV+0.02%/−0.26%, controls−0.96%/−0.07%.
Range+5.92%/+4.66%, controls+0.37%/+0.47%, CPU+5.18%/+2.98%. These justified
the documented construction refinement. They remain complete in initial/; no
source improvement is inferred by subtracting independent initial/refined clocks.

CPU is external Linux TGID user+system through response, weighted over nine
batches, including runtime/JIT/response. Warm timing includes staging/reset,
actual I/O, complete output/file checks and previous-result cleanup; final result,
cached program, worker and fixture teardown is outside batches and joined.
Compilation/startup excluded. No external baseline or removal-fraction claim.

## Allocation, regressions and qualification

Separate existing complete-job driver:192 warmups/image, nine batches of32 jobs,
isolated Lython images sharing CoreLib/GC and precise process allocation counting
across worker threads. Its clocks are diagnostic only. Final allocation/run:

| Complete case | Baseline bytes | Candidate | Change | Unchanged control |
| --- | ---: | ---: | ---: | ---: |
| Text/helpers |3,469,666|3,420,481|−1.4176%|0.0000%|
| Search/early exit |298,559|298,503|−0.0188%|0.0000%|
| CSV/helpers |5,905,615|5,905,621|+0.0001%|−0.0002%|

Initial allocation results agree; all raw counters remain. Full goldens, image
and hash witnesses, reusable balances and final zero balances pass. No retained
storage is refunded early. Initial timing155.107s/allocation30.729s; final timing
151.539s/allocation31.843s. All four owned services exit0/inactive, shared lease free.

Final focused Release252 internal/1,084 public tests PASS on each actual.NET11/10:
loops/comprehensions, ranges, mutable containers, iterators/generators, native and
protocol failures, real suspension, awaited cleanup, ownership/refusal/cancellation
and managed stack entry. No timeouts or containment assertions relaxed. Active
cancellation tests preserve interruption/semantics without enforcing exact pull
positions; old cadence-specific names and unused checksum removed.

Two disposal regressions FAIL on the frozen unchanged137b563a baseline: disposed
cursors wrongly reacquire their source. They PASS on final code. Tests run against
an isolated old-core image with the same new test assembly; no restored-source
incremental build ambiguity. Initial eager native acquisition caused21 existing
failures, including async precedence; restoring lazy acquisition fixed them.
Broader checks then caught6 original-subject liveness failures in mixed cleanup.
Those original tests remain unchanged; terminal state is separate from roots
needed until actual async disposal. All failures and corrected runs are retained.

This source is focused-qualified. Full platform/stress/installed-consumer delivery
will be batched with subsequent retained runtime work. Cached full GREEN41519435155
qualifies90785737/B123+B124 only. No repeated unchanged qualification or package
publication. Raw observations, scripts, sources, failure logs and assessments are
preserved; decompress into the named retrieved directories to reuse analyzers.
