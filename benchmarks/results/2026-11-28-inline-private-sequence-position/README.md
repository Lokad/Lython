# B1969 Keep an exact sequence position in the private generator operand

An exact list or tuple keeps its position in the existing loop slot
through a completed private `sum` reduction. If the expression needs
a full frame, the operand materializes a single iterator at the live
position before the frame exposes `.0`. The preflight covers that
iterator from construction. The private route retains the post-entry
managed stack check and per-instruction cancellation. Range acquisition
still uses the ordinary iterator.

Each 2,048-row tuple or list shipment allocates 114,632–114,688 fewer
CLR bytes in both APIs, about 112 KiB. Peak admission, compiled admission
and growth requests match for all four workloads. The range observation
differs by 56 CLR bytes, and the 128 retained `.0` aliases allocate the
same CLR bytes at the same 280,064 B execution peak. All owners return to
baseline. A public witness checks list growth, `next`, distinct yielded
values, shared iterator identity and exhaustion after promotion.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 411 public and
41 white-box focused Release checks. The two 32-file images use
`83042273c` and the candidate patch. Only the core DLL differs. Both
images pass four independent CPython 3.13.16 output and file goldens in
each collection. The procedure archive preserves the initial allocation
witness from before stack review and the final build's full measurements.

## Local matched comparison

November 28, 2026 UTC on Ubuntu WSL and .NET 11 RC3. Each run uses two
workers per image with at least five seconds of warmup and seven rotated
batches. The repeat reverses worker and workload order and moves from
logical CPU 27 to 25. The warm boundary includes mediated RAM-backed I/O,
output and file verification, and previous-result disposal. Process CPU
also includes framing and fixture reset. Ranges span worker median pairs.

| Async workload | Initial elapsed | Initial CPU | Repeat elapsed | Repeat CPU |
| --- | ---: | ---: | ---: | ---: |
| Tuple generator shipment | −15.30% to +1.89% | −12.97% to −0.45% | −1.56% to +5.91% | −3.17% to +6.93% |
| List generator shipment | −3.60% to −1.26% | −6.23% to +0.60% | −3.11% to +4.93% | −2.86% to +7.73% |
| Materialized range control | −0.37% to +6.85% | −2.08% to +2.08% | −2.39% to +3.59% | −1.44% to +1.62% |

Initial tuple reference CPU worker spread is 10.26%. Repeat list
candidate CPU spread is 9.62%. The tuple CPU improvement and the first
range elapsed increase remain inconclusive. Keep both observations and
the measured allocation reduction. The workers join and remove RAM
fixtures, and both persistent archives verify all 168 batches.

[Allocation and admission](allocation-summary.json) · [Growth requests](admission-summary.json) ·
[Initial timings](summary.json) · [Repeat timings](repeat-summary.json) ·
[Initial images](identity.json) · [Repeat images](repeat-identity.json) ·
[Initial observations](observations.tar.xz) · [Repeat observations](repeat-observations.tar.xz) ·
[Procedures](procedures.tar.xz).
