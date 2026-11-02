# Closed native call contexts — B124

2026-11-02; baseline090574d1, candidate patch and exact loaded hashes retained.
This is an allocation/construction simplification, not a demonstrated general CPU
improvement or a CPython comparison. Publication remains postponed.

The compile-only census found that the canonical CSV/JSON/text/search helpers
already use native-return expressions. Equivalent local-assignment variants use
the ordinary executable engine; their timing remains unmeasured. Final-slot
argument binding and direct builtin methods already existed. No new argument
arena, mutable dispatcher, method-descriptor framework or native statement VM.

After the existing complete native proof succeeds, closed module definitions use
their defining module's state/globals without allocating an unobservable function
ExecutionContext. Binding, function/interpreter depth and managed stack admission
remain paired. Class/nested/annotation/closure/protocol cases retain their lexical
contexts. The evaluator is shared; its unused executable-slot restoration is gone.
Code-owned scope facts govern name lookup. Both invocation APIs and direct calls
share this selection. No cached token, new owner, pool, public API or budget policy.
All guards precede work; failure after work/admission never retries another route.

## Complete-script observations

Quiet Azure F4as_v7, Ubuntu24.04/.NET11RC3, ordinary workstation GC/tiering.
Existing comparison workers/protocol, independent candidate/baseline/unchanged
control, four-second warmup each and nine interleaved ~200ms batches, two blocks.

| Warm script | Elapsed change, blocks0/1 | Process CPU change | Unchanged elapsed / CPU controls |
| --- | ---: | ---: | --- |
| CSV helpers |−0.12% /−2.63%|−0.46% /−1.60%|+0.99%/+1.55%; −1.46%/−3.40%|
| JSON-lines helpers |−1.54% /−0.93%|−1.27% /−2.68%|−0.95%/−3.56%; +0.39%/−1.12%|
| Text helpers |+5.36% /+0.95%|+3.92% /−2.13%|+3.34%/+4.87%; +1.01%/−4.26%|
| Search/early exit |+2.43% /−1.04%|+1.29% /−8.06%|+1.16%/−1.84%; −1.41%/−1.18%|
| Report control |−2.92% /−3.12%|−1.71% /0.00%|−0.87%/+0.64%; +0.06%/−1.60%|

No consistent general elapsed/CPU improvement is established. Keep the adverse
text/search observations: the text first-block elapsed is about2% above its
unchanged control, while CPU tracks the drift. Search CPU improvement appears in
only one block. Unaffected report clocks also improve, so they establish variance,
not a causal gain. Allocation simplification is retained; substantial whole-script
performance and earlier regression debt remain open. No confirmation retiming
was performed merely to remove adverse observations.

Warm timing includes fixture staging/reset, actual mediated I/O, complete stdout/
file checks and prior-result disposal. Startup/compilation excluded. Last result,
cached program, worker and fixture cleanup is outside batches and joined. CPU is
external Linux TGID user+system through response, including runtime/response/JIT,
not guest-only CPU. These measurements do not change external CPython ratios.

## Allocation and correctness

Separate existing complete-job diagnostic: isolated images sharing CoreLib and
process GC,192 warmups/image, nine batches of32 public invocations, precise
process-wide allocation across worker threads. Its clocks are diagnostic only.

| Complete script | Baseline bytes/run | Candidate | Reduction | Unchanged control |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers |6,592,734|5,905,637|10.42%|+0.0040%|
| JSON-lines helpers |6,181,287|5,492,943|11.14%|+0.0015%|
| Text helpers |3,985,791|3,469,666|12.95%|−0.0005%|
| Search/early exit |470,767|298,545|36.58%|0.0000%|

All goldens, image/hash witnesses, quiet gates, zero final balances and joined
cleanup passed. No reservation/refund contract changes: escaping values retain
their existing owners. Timing190.194s; allocation43.892s; both owned services
exit0/inactive and shared lease free.

Focused Release332 public/58 internal pass on each actual.NET11/10. Coverage
includes function binding/defaults, live module slots, defining builtins, aliases,
imported source/tracebacks, closures, real suspended protocols, cancellation,
terminal refusal, budget reuse and stack protection. Four new goldens independently
verified with isolated CPython3.13.2. Initial import test omitted explicit module
authorization/source path; its two failures and correction are retained. Initial
golden harness needed Windows CRLF normalization; no semantic discrepancy.

Full platform/stress/installed-consumer delivery is batched with B123. Focused
proof does not stand in for that checkpoint. Raw observations, source patch,
census, scripts, exact identities, assessments and failure logs are preserved here.
Decompress observations into their named retrieved folders for existing analyzers.

Subsequent delivery: FULLY QUALIFIED by cached terminal41519435155 at90785737.
Windows/Ubuntu ordinary Release.NET11/10, original Stress both and installed
consumers PASS. [Delivery receipt](../2026-11-02-native-call-contexts/delivery-ci-status.json).
This covers the retained source here; later prototypes require their own checks.
