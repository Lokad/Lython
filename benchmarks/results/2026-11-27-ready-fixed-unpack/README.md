# B1852 Return ready fixed-unpack outcomes directly

[B1851](../2026-11-27-paired-shipment-api-cpu/README.md) finds 98 nearest
fixed-unpack samples in async and 50 in sync on the same wide shipment
script. The candidate returns the ready zip count and resulting loop
decision directly. The existing pending pull and continuation still own a
partially filled row. The buffered producer is shared with tuple-producing
zip. CSV keeps its existing producer and shares direct interpreter completion.

Both Windows Release targets pass 1,032 public and 322 white-box checks.
All three scripts match CPython output and files on Windows and on the VM
for both images. Peak admission matches and the budgets return to baseline
in both APIs. The largest CLR allocation difference is 56 B per complete
invocation, on bound async shipment. The paired images differ only in the
core DLL, based on production `9cee6f423`.

## Quiet timing

November 27, 2026 UTC. Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds each and supply seven
rotated batches. Ranges span all pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, verification and previous-result disposal.
Process CPU also includes fixture reset and request framing. Compilation
and startup sit outside both clocks. The file-fed map contains no zip loop.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Wide shipment | Async | −0.25% to +3.60% | +0.29% to +3.41% |
| File-fed map control | Async | −5.66% to +4.64% | −7.04% to +3.52% |
| Bound shipment | Async | +0.15% to +6.86% | −0.39% to +6.40% |
| Wide shipment | Sync | −1.21% to +3.74% | −0.88% to +4.22% |

Withdraw the prototype. It removes two completed result carriers, but
the targeted async workload uses more CPU in every observed pairing.
Bound async elapsed also rises. The file-fed control has more than 5%
CPU spread per image, and the sync result spans zero. The observations
leave the exact source-level cause unresolved. The current fixed-unpack
path remains in delivery. Keep the patch and the raw observations.

The targeted iterator-composition run passes another 72 checks per target,
including suspended fixed unpack, strict tails and cancellation. These
filters can overlap the broader checks. All 112 timed batches and both
quiet gates pass. Staging reuses 32 reference files and 31 candidate
files from B1847. All workers join, the RAM-backed fixtures are removed
and the persistent archive verifies.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patch and checks](procedures.tar.xz).
