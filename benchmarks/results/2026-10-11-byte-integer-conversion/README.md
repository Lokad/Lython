# Byte-to-integer conversion and ownership, 2026-10-11

`int.from_bytes` repeatedly shifted and combined growing magnitudes, then returned
an integer without owning its heap payload. It now borrows immutable byte storage,
preflights the result, converts once through `BigInteger`'s span constructor and
funds the actual returned box through the existing ownership helper. Twelve net
production lines disappear, including copying/reversing the byte input and manual
signed correction. The short conversion kernel's median falls 98.68%/98.81% in
two passes; the second reference pass is noisy. These are diagnostic timings.

## Sources and demonstrated defect

The previous green delivery is `368d0017c41fa5eda9cf48322cf6b618c05399f0`.
Baseline diagnostic producer `128ce11666beab806f5c3fcd46ae6d7929b5da01` preserves
its production tree `fed483544e408ec958b2c9f239cbf3bfbcc06f20` and adds independent
regressions. Candidate implementation is
`80a6f1594272040ea5ef1b80fb0fd0a98db90622`, production tree
`ecf58aae9c7e49ecbc21ca08e78c09eb5ac831e5`, tests tree
`a51670c36697a84e6973572da70934e6dac9073b`. Benchmark/probe sources are unchanged.
[evidence.json](evidence.json) preserves source and actual binary identities, TRX
hashes, raw protocol attempts, prospectively declared controls and cleanup.

Two public programs retain 2,000 distinct 512-octet magnitudes under a 256 KiB cap:
unsigned little-endian and signed big-endian. The baseline succeeds in both real
execution modes at an accounted peak of 165,932 bytes, although the retained
magnitude storage alone exceeds 1 MiB. This is source-supported underaccounting,
not a measured CLR-heap figure. Six ownership/preflight/refund checks and four
public retained-payload checks fail on the original production; two dropped-stream
controls pass. Frozen baseline Release reproduces exactly those ten failures.

The candidate denies the original retained programs within the unchanged cap.
Four thousand dropped conversions still complete under 256 KiB; escaped results
remain charged after their input disappears. The actual returned box is tracked,
and failed tracking refunds only its fresh payload. [SPEC.md](../../../SPEC.md)
documents correctly charged retention and conservative byte-length preflights,
which can require extra headroom for heavily sign-padded input.

## Correctness

- Full matching-Probe Debug: **9,988 passes** (1,929 white-box, 8,059 public).
- Final focused Debug and frozen candidate VM Release: **66 passes** each.
- Frozen baseline Release: **52 passes and ten expected original failures**.
- Both producers verify all four selected workloads and match four independent
  pure CPython cases in each execution mode. Candidate local Debug matches too.
- Cases cover signed/unsigned endian, empty/padded input, octet index coercion,
  iterable input, bool conversion, source independence, denied preflight/refund,
  exact box ownership, escaped aliases, dropped reclamation and retained denial.

General iterable byte materialization and numeric member reboxing remain separate
ownership/preflight investigations. Mutable scalar identity maps and exhaustion
GC relief remain active.

## Short controls

Two fixed passes of four controls produce **eight collections, 347 protocol
attempts and 168 measured batches**. Every measured group finishes in **7–8 seconds
including cleanup**, below its 30-second cap. Independent correctness groups take
4/2 seconds. Workers retain ordinary tiering, GC and all protection settings; the
supervisor's tiering override is removed before worker launch. Zero full lanes or
retry collections are used.

Candidate median change relative to baseline; negative means faster:

| Control | First pass | Second pass |
|---|---:|---:|
| Empty entry | −0.76% | −0.65% |
| Integer loop | −0.89% | −2.26% |
| CSV discard | −3.32% | −5.63% |
| 128 conversions of 512 bytes | −98.68% | −98.81% |

The conversion program uses identical source, bytes and checksum for both Lython
producers and CPython. Whole public-job medians, in microseconds:

| Pass | Baseline Lython | Candidate Lython | CPython |
|---|---:|---:|---:|
| First | 10,200.39 | 134.62 | 40.39 |
| Second | 11,592.10 | 138.51 | 42.25 |

Candidate conversion IQR/median is 1.55%/2.00%; baseline is 1.62%/174.32%.
The second baseline has two 47–50 ms batches. All fourteen baseline measured
per-job samples are at least 10.01 ms, while all candidate samples are at most
0.143 ms. That separation supports the bounded implementation change; strict
whole-workload qualification and broader CPython ratios remain milestone work.
CSV has broad spread, so its lower medians do not establish a CSV improvement.
Allocation bytes are absent from this timing protocol; no byte-saving figure is
assigned to the removed copy and repeated assembly.

All recorded workers are absent, measured groups empty, preparation/assessment
parents stopped, frozen sources clean, binaries rehashed and the VM lease free.
The initial final-audit command attempted to stop an already unloaded preparation
unit; the corrected audit handles absent units and preserves that failed receipt.
No build or measurement is restarted for that audit correction.
