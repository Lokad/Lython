# B1837 Return ready async starmap rows directly

The candidate on `6c459bbd8` replaces the unconditional async advance
with a direct prefix for ready rows. When a source suspends, one continuation
can finish the callback or drain an arbitrary row. The leased drain and its
argument packet stay funded through callback completion. Source order,
strict zip checks, reentry and alias handoff use the existing operations.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 210 public and 23 white-box
Release checks. Coverage includes genuine source and callback suspension,
cancellation, refusal, short zip reentry and retained aliases. All four
complete scripts match CPython 3.13.2 output and file goldens. Peak admission
matches across both APIs and all runs return to the shared budget baseline.
Median CLR allocation differs by at most six bytes per invocation in the
separate Windows diagnostic.

## Local timing

Two workers per image ran on one logical CPU in local Ubuntu WSL .NET 11.
Only the core library differs between images. Each worker warmed for five
seconds and contributed seven rotated batches. Ranges cover all four pairs
of worker medians. Warm elapsed includes mediated I/O, verification and
prior-result disposal. Process CPU also includes fixture reset and framing.
Compilation and process startup are outside these lanes.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-field shipment | Warm sync | −5.40% to −0.13% | −5.11% to −0.29% |
| File-fed map | Warm async | −4.99% to +8.48% | −4.89% to +7.96% |
| Bound zip shipment | Warm async | −2.47% to +1.29% | −0.53% to +2.78% |
| Five-field shipment | Warm async | −1.31% to −0.04% | −4.37% to +0.93% |

The synchronous lane also moves despite using the same starmap path, and
the file-fed map reference workers differ by 11.25% in process CPU. The local
timing leaves the whole-script effect unresolved. Keep the direct completion
path and its single continuation for a suspended source and callback.
All 112 timed batches verify. Workers join and guest fixtures are removed
before the persistent archive verifies.

[Images](identity.json) · [Medians and spreads](summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Raw observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Exact patch and checks](procedures.tar.xz).
