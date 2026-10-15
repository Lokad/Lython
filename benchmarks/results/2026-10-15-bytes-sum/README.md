# Exact byte sums with a machine-integer subtotal

`sum` now iterates exact immutable bytes directly when the start is an exact
builtin integer or Boolean. A local `long` subtotal cannot overflow for a CLR
byte-array length: even the maximum length totals less than 2^39. The exact
start is added once with `BigInteger`, preserving overflow/promotion and signs.
This removes byte enumerators, per-item integer boxes and repeated arithmetic
from this path. Guest integer representation is unchanged.

Both public execution APIs use the same synchronous helper. It holds a span only
inside that helper, retaining the existing native-operation source/result owner
and binder cleanup. Dynamic starts, callbacks and other iterables keep their
ordinary synchronous/asynchronous paths without replay. Empty input returns the
original start. Checkpoints match generic iteration at pull zero, every 64 pulls
and the terminal pull, including empty input.

The existing integer-result preflight and temporary reservation cover the final
magnitude before allocation, including input/result overlap. A conservative
preflight may refuse earlier at narrow allowances. Retained aliases stay funded;
no scalar identity ledger, tracing, weak reclamation, forced GC or numeric ABI.

| Complete copy/checksum/JSON-manifest job, warm synchronous execution | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Two files / 4,107 input bytes | 6.394 ms | 6.080 ms | 0.4382 ms |

The observed 4.9% lower median is within baseline/candidate IQR-to-median
6.7%/11.7%; CPython spread is 1.2%. **Timing remains inconclusive**: no qualified
speedup, confidence interval or overall multiplier. Keep the allocation and
preflight improvement without attributing the complete job cost to sum. One
[diagnostic](warm.json) preserves seven rotating batches, calibration and warmup.
No retry, retuning or replacement samples. The owned command took 8.326 seconds
including cleanup under a 30-second bound.

The existing [ordinary script](../../scripts/file-manifest.py) lists a directory,
reads/copies binary files, computes checksums and publishes a sorted JSON manifest.
Both worker timers include matched disk input/output staging, fresh state,
execution/projection, exact output-file checks and prior-result cleanup.
Compilation, process startup, transport and full response hashing are excluded.
The comparison host supplies efficient file overrides. Actual asynchronous
protocol/host suspension is a correctness check, not an injected-delay speed ratio.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GC/GIL, `-I -S -X utf8` and explicit UTF-8 protocol I/O. Lython's ordinary
policies, allowance and GC/tiering remain; CPython has no equivalent containment.

Baseline is 64fecd53e2b01efbbeef621f0ad71d851459d97c. One modified production
source is frozen in [the declaration](comparison-declaration.json). The catalog,
timing/execution tooling, Python worker and script are unchanged.
[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent stdout and output-file bytes at all three existing sizes.
[Cleanup/source proof](cleanup-source-proof.json) confirms seven workers/fixtures
cleaned, exact source differences, identical tooling, inactive services and lease
availability.

Eleven [independent CPython 3.13.2 programs](cpython-goldens.json) cover bytes,
Booleans, signed/unsigned overflow, huge/negative starts, empty aliases, keyword
binding, invalid starts, user iteration, nested custom addition and numeric
fallbacks. Both public APIs return allowance to baseline. Real host I/O and
cancellation in a dynamic start remain awaited without repeated writes. Retained
large results stay funded across a subsequent execution and release on cleanup.

Fifty-six native comparisons against generic iteration verify fuel, spans, errors
and terminal checks at empty/1/63/64/65/127/128 lengths through both drivers.
Allocation controls sum 8,192 bytes within fixed overhead; magnitude refusals
allocate less than 32 KiB before denying a 64 KiB result. Repeated denials and
cancelled calls refund native ownership without losing inputs.

All 2,292 ordinary white-box tests pass in 11.725 seconds including cleanup.
Focused groups pass 76 white-box, 30 public sum/cancellation and 269 public
governed/representative/file-worker controls; groups overlap, all under 30 seconds.
[Validation](validation.json) preserves an initial new-test assumption that
`bytearray` was available: the builtin is currently undefined. That test now uses
supported user iteration; buffer APIs were not expanded. `memoryview` was not
reached or qualified by that failed setup. No assertion or timeout was relaxed.
No full stress or local full-public rerun; platform/package gates follow at delivery.

This closes one exact builtin iteration family and one scoped accumulator.
Generic arithmetic, mutable containers, other casing/JSON/frontend work remain open.
