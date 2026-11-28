# B1934 Keep iterator callback returns in executable slots

`map` and `starmap` now use one ready-or-pending iteration result.
Native and frame-backed Python callbacks produce the same executable
slot. A callback can return an unobserved scalar through frame cleanup
and a genuine suspension without boxing it and reconstructing the slot.
An object-based callable still converts its result at the iterator
boundary. Callers that need an actual Python object retain the normal
materialization and alias rules.

The reference is `6bd0adf17`. Only the Lython core DLL differs between
the complete 32-file images. Both pass five independent CPython 3.13.16
output and file goldens. Focused Release passes 1,379 public and 776
white-box checks per target on Windows .NET 10 and 11 and Ubuntu WSL
.NET 11. The new tests cover observed identity, implicit `None`, real
host suspension, cancellation, terminal refusal and budget reuse.

## Allocation

The existing Windows .NET 11 allocation witness uses five workloads
through both APIs. Ordinary helper and generator reduction allocates
65,536 B less in `Run` and 65,568 B less in `RunAsync`, about 1.59%.
The protected CSV callback and the file-fed map have identical
allocation in both APIs. Iterable and bound shipment differ by at most
230 B. Compiled charges and execution admission peaks match throughout.
Every command returns the borrowed allowance to baseline.

## Warm timing

November 28, 2026 in Ubuntu WSL and .NET 11 RC3, pinned to one logical
CPU. Two workers per image warm for at least four seconds and supply
seven rotated batches. Ranges span pairings of worker medians.
Elapsed includes applicable RAM-backed I/O, verification and prior
result disposal. Process CPU also includes fixture reset and framing.
Compilation and startup sit outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | `RunAsync` | −1.47% to −0.13% | −1.40% to +0.35% |
| Protected CSV callback | `Run` | −0.37% to +2.81% | −3.33% to −0.85% |
| File-fed map control | `RunAsync` | +4.20% to +6.77% | +2.55% to +3.81% |
| Bound shipment | `RunAsync` | −6.96% to +1.64% | −5.71% to −0.76% |
| Protected CSV callback | `RunAsync` | +0.41% to +1.25% | −0.43% to +0.66% |
| Iterable shipment | `Run` | −1.13% to +1.12% | −2.20% to −1.27% |
| Ordinary helper and generator reduction | `RunAsync` | −7.46% to −6.10% | −8.41% to −2.46% |
| Ordinary helper and generator reduction | `Run` | −2.62% to +1.87% | +2.48% to +2.78% |

The first collection verifies 224 batches. A four-lane repeat reverses
worker placement with the same complete images and catalog and verifies
112 batches. Both runs join all workers and verify the observation archive
after fixture removal.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary helper and generator reduction | `Run` | −0.53% to +6.06% | −2.31% to +0.85% |
| Ordinary helper and generator reduction | `RunAsync` | −11.24% to −3.75% | −8.40% to +0.67% |
| File-fed map control | `RunAsync` | −34.20% to +6.08% | −41.46% to +1.45% |
| Iterable shipment | `RunAsync` | +2.26% to +3.12% | +0.71% to +1.94% |

Retain the typed return path for the removed object transport, verified
allocation savings and repeated elapsed reduction in the ordinary async
script. Its elapsed falls 6.10 to 7.46% initially and 3.75 to 11.24% on
repeat. CPU spans zero in the repeat. The ordinary sync CPU increase also
spans zero on repeat.

Iterable async elapsed falls 0.13 to 1.47% initially and rises 2.26 to
3.12% on repeat. The repeat has tight CPU spreads and a 0.71 to 1.94%
CPU increase. Carry that regression into the remaining shipment work.
The file-fed control uses 2.55 to 3.81% more CPU initially. Its reference
worker spread exceeds 63% in the repeat, leaving the control effect
unresolved. Preserve both observations. This comparison covers two
Lython revisions. [CPython checkpoint](../2026-11-28-current-cpython-regex-checkpoint/README.md).

[Timings](summary.json) | [Allocations](allocation.json) | [Images](identity.json) |
[First observations](observations.tar.xz) | [Repeat timing](repeat-summary.json) |
[Repeat observations](repeat-observations.tar.xz) | [Patches and checks](procedures.tar.xz).
