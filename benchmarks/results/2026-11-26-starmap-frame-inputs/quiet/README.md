# B1804 Quiet starmap callback confirmation

The [initial source pair](../README.md) ran unchanged on November 26,
2026 UTC, on Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both
32-file images and all four independent CPython 3.13.16 output and file
goldens verified on the VM before timing. Each of two workers per source
warms for five seconds and supplies seven rotated batches targeting
440 ms. Ranges span all pairings of worker medians. Negative changes
mean less time.

Warm elapsed includes mediated RAM-backed I/O, output and file checks,
and previous-result disposal. Process CPU also includes fixture reset
and request work. Compilation, startup and final teardown sit outside
the measured lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-item shipment rows | Warm sync | −6.64% to −0.15% | −8.09% to +0.16% |
| Five-item shipment rows | Warm sync | −2.75% to −1.76% | −4.82% to −2.14% |
| Zip-to-starmap control | Warm async | −3.15% to −1.06% | −3.74% to −0.95% |
| Five-item shipment rows | Warm async | −9.02% to −0.87% | −11.42% to +0.98% |

Retain the change for the removed argument packet and the measured
20.2% allocation reduction in both APIs. Five-item sync elapsed falls
1.76–2.75% and process CPU falls 2.14–4.82%, reversing the local sync
slowdown. Async elapsed falls 0.87–9.02%. Async CPU spans zero and the
candidate CPU spread is 10.04%. The zip control improves in both clocks,
and the three-item control also takes less elapsed time. The source pair
supports the allocation result, with favorable quiet elapsed whose exact
attribution remains uncertain. Preserve the original local adverse results.

The recorded fixture tapes show that the callback is already eligible
for closed operand returns in all three shipment scripts. Its selected
path reads the flag and one returned parameter. The five-item version
has six parameter slots including the receiver and a ten-slot workspace.
The current prototype initializes the workspace and fills the whole row
before that existing return evaluator runs. These source facts identify
an available handoff to remove without attributing a measured CPU share.
[Callback tape census](callback-tape-census.json).

All 112 timed batches and both machine checks pass. Workers join, guest
fixtures are removed and the persistent archive verifies. Two duplicate
core DLLs from B1710 and B1711 and four expanded B1512 profile files
provided 12,541,952 B. The original preview compared
B1512 with its corrected attempt and stopped before changing storage.
The corrected preview matched the original attempt and its published
archive. Full frozen local images and compressed core backups verified
before retirement. All three published and on-VM archives still match.
Staging finished with 6,705,152 B free before collection.

[Image identity](identity.json) · [Timing and spreads](summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification, storage custody and tape procedure](procedures.tar.xz).
