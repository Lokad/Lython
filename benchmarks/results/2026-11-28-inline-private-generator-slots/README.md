# B1975 Keep compact private generator slots inline

The existing native instruction loop runs a compact list or tuple
generator in a fixed managed inline span. The generator retains its
original preflight for the array, frame shell and outer iterator. One
array receives the current live slots and instruction position if native
execution stops. Its ordinary prefix can finish privately or transfer the
same storage and the iterator position to a full frame. A shared definition
lookup range and the active exception follow the ordinary owner.

The revised candidate allocates 278,298–278,528 fewer CLR bytes per
2,048-row tuple or list shipment in both APIs, about 272 KiB. Range,
explicit-loop and iterable allocation changes stay within 118 B.
The 128 retained iterator aliases have identical CLR allocation and peak
admission. All six cases have matching execution peaks, compiled admission
and growth requests, and each returns the shared allowance to baseline.

The original prototype promoted immediately when native execution stopped.
Two new bounded exponentiation witnesses pass on the reference and fail on
that prototype. The revised candidate completes both within the original
16 KiB allowance and refunds it. The discarded numeric-key fixture exceeded
that allowance on the reference too. Focused Release checks cover that
continuation, exception chaining, promotion, cancellation during host
I/O and retained iterator aliases. Windows .NET 10 and 11 and Ubuntu WSL
.NET 11 each pass 415 public and 41 white-box checks.

## Quiet warm async comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Both matched comparisons use the frozen production `486bad1ee` reference.
The archived source patches apply to `d3dfed3a2`, whose production and
test sources match that reference. In each pair the 32-file images
differ only in the core DLL. Both images verify six CPython 3.13.16
output and file goldens locally and on the VM.

Two workers per image warm for at least five seconds and run seven
rotated batches each. Elapsed includes mediated RAM-backed I/O,
verification and previous-result disposal. Process CPU also includes
fixture reset and request framing. Both exclude compilation and launch.
Ranges span pairings of worker medians.

| Workload | Prototype elapsed | Prototype CPU | Revised elapsed | Revised CPU |
| --- | ---: | ---: | ---: | ---: |
| Tuple generator shipment | −4.58% to −3.09% | −3.52% to −2.66% | −7.27% to −2.92% | −7.43% to −2.40% |
| Explicit loop control | −3.98% to +4.27% | −3.40% to +5.57% | −3.06% to +6.26% | −2.35% to +6.13% |
| Iterable shipment control | −0.46% to +0.21% | 0.00% to +1.13% | | |

The revised collection reverses worker and workload order for the
two repeated lanes. The explicit-loop CPU effect remains inconclusive.
Both collections pass both quiet gates and verify all 140 timed batches.
All workers join and the RAM-backed fixtures are removed before releasing
the persistent archive.

[Revised summary](summary.json) · [Prototype summary](initial-summary.json) ·
[Revised image](identity.json) · [Prototype image](initial-identity.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Observations](observations.tar.xz) · [Prototype observations](initial-observations.tar.xz) ·
[Source patches and checks](procedures.tar.xz).
