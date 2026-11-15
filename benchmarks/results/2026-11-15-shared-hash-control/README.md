# Shared-process text method control

R391/B779, 2026-11-15 UTC. Qualified **810379be / 8fbfa3f0** versus unchanged
R390 candidate **f304b3db**. Two processes each load three distinct images using
the existing complete-job driver: reference, candidate and unchanged reference.
Images share CoreLib, process GC and matching hash witnesses; witnesses differ
between processes. All complete-script goldens and ownership balances pass.

| Process | Candidate invocation change | Full-request CPU change | Unchanged reference invocation spread |
| --- | ---: | ---: | ---: |
| 1 | −2.43..+5.20% | −5.71..+0.47% | 7.82% |
| 2 | −5.35..−3.74% | −8.28..−7.28% | 1.68% |

Five rotating rounds, 256 jobs per batch, eight full-request warmup seconds per
image; 63.806 s total collection. Ranges compare candidate medians against both
references, not confidence intervals. Process 1 remains inconclusive even with
identical hashes. Hashing alone cannot explain earlier variability. Process 2 is
favorable; it does not erase [initial adversity and confirmation](../2026-11-15-invocation-lifetime/README.md).
Allocation is effectively flat in this diagnostic; no allocation reduction claimed.

Decision: retain the complete-invocation lifetime simplification for delivery,
with text uncertainty explicit. The [earlier file-search comparison](../2026-11-15-invocation-lifetime/quiet/summary.json)
supports lower wider CPU. Preserve asynchronous caller publication, cancellation
state/token, genuine suspension, first refusal and funded cleanup. No general or
portable speedup claimed. Full platform/stress/package qualification follows;
this closes the bounded R390 measurement decision, not the performance objective.

Quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs, .NET 11 RC3 and ordinary
workstation GC/tiering. The invocation clock measures awaited public RunAsync and
its native cleanup; it excludes public-result disposal, reset and full goldens.
Full-request elapsed/CPU include those costs. This differs from command-v2 clocks;
do not mix their absolute times. Shared process GC and noncollectible images remain
limitations. No startup, pending-I/O latency, heap/RSS or embedding-tail claim.

Exact source/image/tool identity, hash witnesses, goldens, zero balances, quiet
gates, process joining and persistent archive verification pass. No users or RAM
fixtures remain; final free space 270,180,352 B, existing floors unchanged. No
runtime image upload or retirement. Original preflight caught local allocation-tool
files absent from the VM worker manifest before staging or collection; the corrected
manifest is retained. No collection was restarted.

[All medians, controls and original samples](summary.json), [raw data and tool source](observations.tgz),
[local build/preparation procedures](local-procedures.tar.xz),
[retention and remaining debt](../2026-11-15-invocation-lifetime/retention-decision.json),
[archive proof](archive-receipt.json), [terminal state](terminal-status.json).
