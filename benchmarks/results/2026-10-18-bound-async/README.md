# Direct forwarding of bound async calls

The shared bound-call async entry now returns its implementation's ValueTask
directly. Its execution check, named binding and presence selection remain.
The method owns no frame, cleanup, projection or storage policy, so its extra
async state machine is removed without adding a helper or runtime mechanism.

This is an earlier-Lython allocation comparison. It establishes no elapsed,
total suspended-execution allocation, CPython, physical-heap or embedding-latency
benefit. Primary external comparisons remain in
[BENCHMARKS.md](../../../BENCHMARKS.md).

| Native entry boundary | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| One list call, prefix before a blocked first pull | 1,280 | 1,128 |
| One sum call, prefix before a blocked first pull | 1,160 | 1,008 |
| 128 completed list calls, synchronous control | 45,056 | 45,056 |
| 128 completed list calls, async control | 54,272 | 54,272 |
| 128 completed sum calls, synchronous control | 12,288 | 12,288 |
| 128 completed sum calls, async control | 12,288 | 12,288 |

Each genuinely pending prefix saves 152 bytes; completed controls are unchanged.
Measured 2026-10-18 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is immutable
`ab6cb90b42760c3e947e72f26b4fac8fa070047b`; candidate is the frozen
[one-method patch](candidate.patch) on that revision. [Validation](validation.json)
records source, assembly and receipt hashes; these are not arbitrary later HEADs.

The [temporary recipe](BoundCallAllocationDiagnostic.cs.txt) prepares the context,
source and native callable outside the counter. Four warmups precede eight
samples. Completed controls call the native entry 128 times; the pending cases
call once and measure current-thread allocations only until it returns a pending
ValueTask. The source supplies a valid synchronous iteration surface for cursor
validation and blocks its actual async first pull on a controlled gate. The task
must be incomplete; the gate is then released and completion/cleanup awaited
before owner disposal. Results, alias identity, cleanup count, frame/reservation
unwind and shared-owner refunds are checked. No requested GC or changed
protections. Compilation, setup and public startup/scheduling are excluded.
[Before](allocation-before.json), [after](allocation-after.json) and
[comparison](comparison.json) data preserve all samples.

All 34 [baseline boundary records](boundary-records.json) match afterward,
including fuel, spans, effects and cleanup. All 1,121 distinct focused Release
cases pass, including ordinary builtin failures/binding, presence-aware gzip/zlib,
actual suspension/cancellation, retained owners, terminal refusal, managed stack
admission and 58 complete CSV/text/JSON/file goldens. Data-driven gzip cases can
share a TRX method ID; validation distinguishes cases by ID and test name. No
permanent tests were added or changed; the diagnostic was removed before final
builds. Original assertions, timeouts and Stress scales remain.

Internal synchronous binding/body failures may now throw while obtaining the
ValueTask instead of faulting a wrapper. Existing outer call-entry try/await
preserves public RunAsync results and Python error/cleanup behavior. Native
implementations retain their own bound storage, and callers retain frames,
operands and owners through awaited completion, exactly once. No public contract,
accounting, regex or dependency changes. Explicit Release-package and platform
qualification are separate delivery records; this precommit artifact does not
claim their completion.
