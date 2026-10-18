# Native string operations through async hosting, 2026-10-18 UTC

Exact builtin string pairs now share the synchronous executable operation path
already used by native numeric operands. This removes nested async operation
helper calls for ordinary string binary and augmented operations. It changes
one existing predicate and its comment. Operator implementations, capacity
preflights, ownership, transfer/cleanup and the general awaited protocol path
remain unchanged. No execution mode, dispatcher, cache or ownership layer is added.

| Native loop, 2,048 comparisons | Before median | After median | After / before | Allocations before / after |
| --- | ---: | ---: | ---: | ---: |
| Async builtin strings | 2.7052 ms | 2.0524 ms | 0.7587 | 115,144 / 115,144 bytes |
| Synchronous string control | 1.9814 ms | 1.8704 ms | 0.9440 | 115,168 / 115,168 bytes |
| Async numeric control | 2.20195 ms | 2.1798 ms | 0.9899 | 115,144 / 115,144 bytes |

The async string median is nominally **24.1% lower** in this single native
capture. All after samples in that series are below every before sample. The
predeclared gate required at least 10% lower async-string median, unchanged
allocations and control medians no higher than 110% of baseline. It passed
without repeated timing. Controls and raw samples remain visible: this is an
unqualified local native diagnostic, not a complete-script, CPython, host-latency
or later-HEAD speedup. The primary external baseline remains CPython in
[BENCHMARKS.md](../../../BENCHMARKS.md); its workload timings are unchanged.

## Source and boundary

Baseline is immutable **7149dac72894055245cf4fb76e2a08aad3ac40ce**. Candidate is the
frozen [one-file predicate patch](candidate.patch), with semantic/containment
coverage extended in two test files. [Validation](validation.json) verifies the
exact two source substitutions and before/after hashes. Existing numeric native
dispatch was already present; this extends it to two exact `PyString` values.
`PyString` is sealed. Mixed operands and guest string subclasses remain on the
awaited route, where protocols can invoke host I/O. Recursive JSON parser
continuations were assessed and retained; no parser change is part of this work.

The temporary [recipe](diagnostic.cs) executes a compiled Unicode comparison
loop inside the white-box assembly. It includes loop, counter, frame and result
work; compilation, context/input construction and reporting are outside timing.
Each role takes four warmups and eight samples. ValueTasks must complete inline
on the same managed thread, permitting per-thread allocation measurement.
Ordinary JIT/GC settings remain; no forced collections, tracing, VM comparison
lane or ten-minute run is used. Shared-owner disposal returns allowance to zero.
The temporary diagnostic is removed from tests afterward.

Machine: Windows 11, Intel Core i7-14700KF, 28 logical processors; .NET SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13. [Toolchain details](dotnet-info.txt),
[baseline](baseline-diagnostic.trx), [candidate](candidate-diagnostic.trx),
[comparison](comparison.json) and process receipts preserve the native evidence.
The changed gate's native timing magnitude is not milestone-qualified.

## Correctness and containment

All **694 distinct final Release cases** pass: 133 white-box, 410 public
semantic/protocol, 89 ownership/admission and 62 complete-job/cancellation cases.
The last group includes 58 complete CSV/text/JSON/file jobs. Original Stress
coverage stays separate; its fixtures, assertions and timeouts were not reduced.

All **44 frozen before/after work matrices** match, including values/types,
errors/messages, execution steps, failure spans and every tested fuel boundary.
The final suite preserves those 44 and adds two matrix-multiplication error
controls. Concatenation, augmentation, Unicode comparison, containment,
formatting and unsupported operators are covered through both APIs.

New concatenation pressure cases prove refusal precedes result publication and
leaves both operands intact. Retained text aliases remain charged while another
execution runs; cleanup returns accounting to baseline. Mixed string/protocol
operands still await left-hand and reflected methods that use host I/O, including
cancellation without replay or premature refund. Existing terminal-admission,
shared-budget, retained-cache and complete-job goldens also pass.

This native comparison is closed; delivery qualification is recorded separately.
It does not attribute the published CPython gap to async helpers
or promise the same improvement in I/O-heavy scripts. Public Python semantics,
managed stack admission and cooperative memory contracts are unchanged.
