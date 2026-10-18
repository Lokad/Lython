# Direct string suffix calls

Ordinary one-to-three-positional `str.endswith` calls now share the existing
`startswith` operand path. Eligible calls avoid receiver-bound callables and
argument/binding arrays. The two existing call wrappers use the semantic
`isStart` flag; there is no new binder, callable class, cache or owner.

This is an allocation comparison with earlier Lython. It establishes no elapsed,
CPython, physical-heap or embedding-latency benefit. The primary external timing
account remains [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm 128-call Unicode loop | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| Immediate `endswith`, synchronous interpreter | 48,640 | 7,680 |
| Immediate `endswith`, async interpreter | 44,520 | 7,656 |
| Exposed `endswith` method, synchronous control | 44,552 | 44,552 |
| Exposed `endswith` method, async control | 44,528 | 44,528 |
| Immediate `startswith`, synchronous control | 7,680 | 7,680 |
| Immediate `startswith`, async control | 7,656 | 7,656 |

Measured 2026-10-18 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is immutable `015ee44a8713223484196f94b9e65424728ad1dd`;
candidate is the frozen [five-file patch](candidate.patch) on that revision.
[Validation](validation.json) records source hashes, runtime identity and test
receipts. These identities do not describe an arbitrary later HEAD.

The [temporary recipe](StringSuffixAllocationDiagnostic.cs.txt) reuses the
bounded-startswith diagnostic. It compiles once and prepares a governed context
with 128 distinct Unicode strings outside measurement. Both methods check the
same character range with `('😀', 1, 2)`; the exposed control materializes its
method before calling it. Four warmups precede eight samples. The counter covers
the executable loop, frames, checks and result, including transport and method
construction. Both APIs must complete inline on the same managed thread.
Compilation, setup, public startup/scheduling and actual suspension are excluded.
No requested GC or altered protections. All results, frame/reservation unwind
and shared-owner refunds are asserted. Raw [before](allocation-before.json)
and [after](allocation-after.json) samples preserve the observations.

The immediate call saves 320 bytes synchronously and 288 bytes asynchronously;
the controls are unchanged. This does not establish a whole-file speedup.

The [40 complete boundary records](boundary-records.json), frozen before
production edits, match afterward: native/guest bounds, validation failures,
effects, fuel cutoffs, error selection and spans. The permanent existing fixture
now covers both methods and checks hashes of these records. Existing direct-call
controls also cover both methods: pool/cache bypass, evaluated receiver retention,
dynamic lookup, suspension and cancellation. Public controls cover Unicode/tuple
matching, exposed/keyword/expanded calls, actual suspended index descriptors,
terminal refusal and retained output funding. All 1,065 distinct focused Release
checks pass, including 58 complete CSV/text/JSON/file goldens and four protocol
cancellation cases. The diagnostic was removed before final builds. No existing
assertion, timeout or original Stress scale changed.

General Python binding remains for exposed methods, dynamic receivers,
keyword/expanded and ineligible call shapes. Guest bounds retain the existing
awaited affix implementation and operands until completion; native bounds stay
synchronous through either API. Existing storage preflights, cleanup and terminal
admission refusal remain. No public contract, dependency or regex change.
Platform and explicit Release-package qualification are recorded separately from
this precommit diagnostic; the artifact alone does not claim their completion.
