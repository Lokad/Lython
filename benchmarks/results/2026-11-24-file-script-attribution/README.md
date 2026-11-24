# Warm file-script attribution (B1616)

The exact B1615 candidate implements `556c8d71c`. The unchanged
comparison worker ran two complete file scripts on local Ubuntu under
WSL with .NET 11 RC3. Both scripts use the verified file goldens and
RAM-backed fixtures. A three-second warmup precedes each six-second
`dotnet-trace` sampled-thread-time capture. The worker completed its
requests, closed and removed its fixtures before the archive was built.

| Inclusive CPU-tagged weight on the execution thread | Configuration manifest | JSON summary helpers |
| --- | ---: | ---: |
| Guest work item | 3,882 ms | 3,418 ms |
| Opened-file acquisition | 840 ms | 635 ms |
| Host methods under acquisition | 728 ms | 565 ms |
| Opened-file reads | 497 ms | 142 ms |
| JSON encoding | 474 ms | 108 ms |
| JSON decoding | 197 ms | 2 ms |
| Native workspace and reachable helper work | 404 ms | 1,827 ms |

The rows overlap and report converted trace weights. They establish
which code runs beneath a frame. Inlining makes native workspace
initialization inseparable from helper execution in this trace. Host
acquisition uses the same path checks, shared admission and file opens
as the external comparison. Fixture reset and output checks run on
other threads and appear separately in the raw attribution.

About 87–89% of acquisition weight sits inside the benchmark host,
which resolves contained paths and acquires concrete file handles.
Preserve host checks and treat those samples as host work. On the
manifest, the JSON encoder is the larger of the two JSON stages.
Exact native `dump` already avoids guest strings for chunks. Its
file loop still resumes the traversal at each chunk boundary. A
next comparison can test whether draining the same traversal into
the existing file sink removes that per-chunk control work while
keeping writes, callbacks and cleanup ordered. This trace does not
identify the fraction of encoding time that change would remove.

[Attribution](attribution.json) · [Raw traces, batches and procedures](observations.tar.xz) ·
[Source and correctness evidence](../2026-11-24-builtin-shadowing/README.md).
