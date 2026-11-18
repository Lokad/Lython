# Shared UTF-8 capture positions

R533/B963 compared qualified 925397f65 (core 2c6b304b) with isolated R533
(core 56a28440) on 2026-11-18 UTC. The quiet Azure F4as_v7 VM ran .NET 11 RC3
and the optional 0.4.0 adapter with default GC and tiering. Both copied 32-file
images passed all four outputs against CPython 3.13.2 during local preflight.
The comparison measures two Lython revisions with the same provider.

Each job used eight seconds of rotated warmup per process, two processes per
revision and five measured rounds. Warm timing includes the script and output
checks and prior-result cleanup. Process CPU also includes worker work outside
the guest timer. Changes are candidate/reference ratios across all four
process-median pairings. Negative percentages favor the candidate.

| 4,096-scalar workload | Warm elapsed | Process CPU | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Unicode interior, 512-scalar captures | −4.86 to −3.41% | −6.60 to −4.81% | 0.61 / 0.91% |
| ASCII interior, 512-scalar captures | −0.95 to +0.07% | −1.89 to +1.92% | 0.16 / 0.86% |
| Unicode suffix captures | +0.33 to +2.57% | +1.72% | 0.41 / 1.81% |
| Short Unicode captures | −0.26 to +0.92% | 0.00 to +2.00% | 0.87 / 0.31% |

All four jobs completed in 199.53 seconds. Accounted peaks matched and the shared
allowance returned to baseline. Allocation differences were below 0.003%. Workers
joined, fixture storage was removed, both machine gates passed and the complete
observation archive was verified. Two exact superseded prototype replicas from
B411/B412 were retired after checking Git blobs, ownership, paths, hashes and links.

Retain the deletion of directional rune decoders and the demonstrated interior
capture improvement. Carry the Unicode suffix slowdown as observed regression
debt. The result concerns capture projection with the current provider.
The old shorter in-flight test and its failure are preserved with the refined
fixture checks. Full delivery applies to the integrated source.
[Initial evidence](../README.md) · [Timing summary](summary.json) ·
[Allocation and reuse](allocation-summary.json) · [Observations](observations.tgz)
