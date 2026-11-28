# B1973 Forward a cached record comparison to a private sum

The candidate passes a successfully fetched field to the existing scalar
comparison and adds its boolean result at the adjacent discarded yield.
This removes two slot publications and two dispatcher entries. The work
stays inside the admitted generator state. A guest-defined comparison
receives the fetched field through the ordinary instruction. A widened
subtotal receives the completed comparison at the original yield.

The earlier closure-forwarding trial leaves the measured generator at
seven slots with the same instructions. Its lexical-slot eligibility
does not include generator expressions. The measured patch retains
their existing closure reads.

The 32-file images differ in the Lython core DLL alone. Both match
three same-input CPython 3.13.16 output and file goldens on both
machines. Execution peaks, compiled admission and growth requests
match in both APIs. All runs return their allowance to baseline.
Local CLR allocation differences are below 256 B per complete run.

Windows .NET 10 and 11 each pass 418 public and 41 white-box focused
Release checks. Ubuntu WSL .NET 11 passes 412 public and 41 white-box
checks with a narrower filter. The added witness exercises dynamic
comparison, mutation of a later field and a subsequent deleted key.
Two warmed EventPipe captures verify 85 sampled batches. The forwarding
helper accounts for 3.6% of resolved Lython samples in the tuple
script. This confirms use of the helper and describes its sampled
cost. The comparison below supplies the elapsed and CPU observations.

## Quiet warm async comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image each warm for at least five seconds, then perform
seven rotated batches. All workloads use the same 2,048 CSV rows.
Elapsed includes mediated RAM-backed I/O, previous-result disposal,
and output and file verification. Process CPU also includes fixture
reset and request framing. Compilation and process startup are outside
both observations. Ranges span pairings of worker medians.

| Workload | First elapsed | First process CPU | Repeat elapsed | Repeat process CPU |
| --- | ---: | ---: | ---: | ---: |
| Tuple generator shipment | −7.91% to −6.39% | −7.83% to −6.57% | −7.43% to −5.96% | −6.57% to −5.44% |
| Explicit loop control | +4.99% to +12.76% | +5.03% to +12.90% | −7.49% to +2.64% | −6.51% to +2.29% |
| Iterable shipment control | −4.22% to −0.68% | −2.80% to +0.75% | | |

The repeat uses the same images with reversed worker and workload order.
Both collections verify both quiet gates and all 140 timed batches.
The tuple improvement repeats. The explicit loop's first CPU increase
spans zero on repeat, leaving its effect inconclusive. Iterable CPU
also spans zero in its measured collection.

[Timing summary](summary.json) · [Worker images](identity.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Repeat summary](repeat-summary.json) · [Repeat observations](repeat-observations.tar.xz) ·
[Managed attribution](profile-managed-attribution.json) · [Raw captures](profiles.tar.xz) ·
[Source patch and checks](procedures.tar.xz).
