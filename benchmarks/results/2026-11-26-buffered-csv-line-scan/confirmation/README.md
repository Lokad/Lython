# B1762 Quiet confirmation of the decoded-window CSV trial

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. The five lanes reuse the [local trial](../README.md) reference
`bc93d4d74`, candidate patch, complete 32-file images and catalog. Two
workers per source contribute seven rotated batches after at least
seven seconds of warmup. Changes span the four pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, output and file
checks, and result disposal. Wider process CPU also covers fixture reset
and request framing.

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment callback report | Async | −1.11 to +2.17% | −0.48 to +2.44% |
| File-fed integer quantity map | Async | +1.39 to +3.85% | +1.48 to +4.86% |
| Unchanged Unicode text report | Sync | −2.28 to −0.90% | −3.61 to +0.03% |
| Quote-heavy CSV policy invoice | Async | −0.49 to +2.85% | −0.75 to +3.44% |
| Shipment callback report | Sync | −2.13 to +0.19% | −0.91 to 0.00% |

Shipment gives no clear improvement. The file-fed map uses 1.48–4.86%
more CPU, with same-source CPU spreads of 1.04% and 2.26%. The invoice
crosses zero. The unchanged Unicode control takes 0.90–2.28% less elapsed
time, and its CPU range crosses zero. The quiet results do not confirm the
large favorable local elapsed ranges for file-fed map and invoice.

Keep the current scanner. The prototype replaces a fast newline search
with a more general scan and can redo part of the work on fallback.
The data do not isolate the file-fed cost to either mechanism. Reopen
the design if evidence identifies a cheaper boundary shared with field
decoding, or a workload exposes a larger previously measured cost. The archive preserves the targeted
mixed-newline and read-position regression with the patch.

Windows .NET 11 and Ubuntu .NET 10 each pass 425 public and 179 white-box
focused checks. Both unchanged images passed independent CPython output
and file goldens before staging. The VM verified the complete candidate
image against the same catalog. Both quiet gates and all 140 timed batches
passed, workers joined, and guest fixtures were removed. The archive
verified under the lease. Two retired remote core copies matched exact
complete 32-file images and raw archives in persistent local storage. The
custody receipt records their digests and the VM headroom.

[Timing](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and checks](procedures.tar.xz).
