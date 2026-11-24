# B1569 Compiled regex index protocols

`RunAsync` now awaits actual guest `__index__` calls for compiled regex
counts and bounds. Three public cases originally returned an
asynchronous capability error. The corrected path preserves count,
start and end order, honors cancellation during a file read and keeps
the execution charge live until the delayed close and cleanup finish.

| Complete warm script | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Direct compiled-pattern log search | −2.98 to +0.59% | −4.54 to +0.90% |
| Saved compiled-pattern method | +0.98 to +2.06% | +0.91 to +1.60% |
| Module log search control | −2.73 to +2.57% | +0.09 to +2.97% |
| Quoted invoice control | −3.34 to +1.03% | −2.70 to +1.26% |

Keep the correctness change. Saved-method calls cost slightly more
in this collection. Allocation differs by at most 490 bytes per
invocation. All four admitted peaks match and allowances return to
baseline. The focused tests pass on Windows and Ubuntu Release for
.NET 11, including the pending-close cancellation and refund checks.

Frozen `d8d638dba` differs from `f9d600fe9` only in the core DLL of
the 32-file images. The regex change is committed as `f524a9c46`
alongside the separate fixed-result change. The dedicated Azure
F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Two workers per source warm for eight seconds and contribute seven
rotated samples each. Warm elapsed includes mediated RAM-file I/O,
golden checks and prior-result disposal. Wider CPU includes reset
and request overhead. All 112 batches match CPython 3.13.16 goldens.
Both quiet gates, worker joins, fixture cleanup and persistent archive
verification pass.

The two initial remote stages stopped before root creation. The first
selected a previously retired replica. The second verified a duplicate
whose size was insufficient. The launch kept the disk floor, verified
two archived duplicates and retired only their extra VM copies.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures, failed stages and exact patch](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
