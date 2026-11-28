# B1933 Initialize fresh executable frames directly

A fresh function, generator, module or class frame now initializes its
executable state in the destination. Both frame initialization and the
borrowed invocation use the shared initializer. Storage admission and
captured-cell construction finish before the initializer publishes the
live code. The entry exception is captured afterward as before.

Release IL confirms that the shared initializer receives the destination
by reference and publishes its fields directly. The whole-state copy and
clear remains in one call site, where a borrowed invocation promotes
already-executed work to a frame. Generator workspace ownership and
cleanup still use the existing frame. This change removes a value
transfer. Frame and workspace allocations remain part of activation.

The reference is the B1932 image from `2ef73bd71`, with the same runtime
source as this trial's parent `ed790f673`. Only the core DLL differs
between the 32-file images. Both pass four independent CPython 3.13.16
output and file goldens. Windows .NET 10 and 11 and Ubuntu WSL .NET 11
each pass 2,652 public and 773 white-box focused Release checks.
Coverage includes saved and bound generators, escaping cells,
workspace isolation, cancellation, terminal refusal and awaited cleanup.

The Windows .NET 11 allocation witness returns the allowance to baseline
on all eight workload/API pairs. Peak execution admission and compiled
reservations match. Median CLR allocation changes by at most 230 B per
execution.

## Timing

November 28, 2026 in Ubuntu WSL on .NET 11 RC3, pinned to one logical
CPU. Two workers per image each warm for at least four seconds and
contribute seven rotated batches. Ranges span all pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, verification and
previous-result disposal. Process CPU also includes reset and framing.
Compilation and startup are outside both clocks. All 196 timed batches
match their expected results, workers join and the persistent archive
verifies after fixture cleanup.

| Script | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | `RunAsync` | −4.58% to −2.29% | −3.95% to +1.14% |
| Bound shipment | `Run` | −7.70% to −1.02% | −3.36% to +1.57% |
| File-fed map control | `RunAsync` | −0.20% to +2.52% | −2.50% to +2.27% |
| Ordinary helper and generator reduction | `RunAsync` | −7.57% to +13.71% | −14.69% to −0.45% |
| Iterable shipment | `Run` | −7.61% to −0.64% | −5.56% to +1.36% |
| Bound shipment | `RunAsync` | −5.57% to −2.07% | −3.79% to +1.83% |
| Ordinary helper and generator reduction | `Run` | −3.89% to −1.12% | −5.88% to −2.96% |

Retain the direct initializer for the removed transfer. Iterable
shipment has lower elapsed in both hosting modes, with CPU spanning
zero. The ordinary helper and generator reduction uses less sync CPU
in this run. Its async candidate workers differ by 14.19% in CPU and
elapsed spans a substantial increase and decrease. The file-fed control
spans zero on both clocks. Keep the mixed results with the exact
allocation and IL evidence. The current async shipment gap remains
open. [External baseline](../2026-11-28-current-cpython-regex-checkpoint/README.md).

[Timing](summary.json) | [Allocation](allocation.json) | [IL evidence](il-evidence.json) |
[Archived profile attribution](activation-costs.json) | [Image identity](identity.json) |
[Observations](observations.tar.xz) | [Patch and procedures](procedures.tar.xz).
