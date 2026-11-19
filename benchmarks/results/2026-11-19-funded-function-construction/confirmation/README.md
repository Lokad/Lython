# B1089 shared function construction confirmation

The frozen B1087 source pair reran with 12 seconds of full-request warmup
per process. Two independent synchronous reused-default blocks reversed
the starting worker order. The asynchronous defaults lane and annotated
rules used the same images. Each block had five measured rounds and
duplicate processes per source.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Reused defaults, warm Run, first block | −2.02 to +1.95% | −2.97 to +0.49% | 2.48 / 1.53% |
| Reused defaults, warm Run, reverse order | −1.87 to −0.38% | −4.00 to +0.63% | 0.71 / 0.80% |
| Reused defaults, warm RunAsync | −0.04 to +2.40% | +0.92 to +2.33% | 0.50 / 1.93% |
| Annotated rules, warm Run | −1.18 to +1.22% | −0.58 to +3.25% | 1.79 / 0.63% |

The initial 3.49–9.90% synchronous reused-default slowdown did not recur.
The annotated timing gain also did not repeat. The warm asynchronous lane
has a small CPU increase and mixed elapsed. The original adverse
synchronous result remains unexplained.

Retain the shared constructor as `eeb10506d`. It removes duplicate
construction logic and one allocation per function. The three rule scripts
allocate 3.71–4.61% less with matching peaks in B1087. Record the possible
asynchronous cost for the next combined delivery comparison. The retained
source delta matches the measured change alongside the independently
retained compiler change `c3873194c`.

Both complete images and the catalog match B1087. All 80 timed batches
verified in 249.77 seconds on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. Workers joined, fixtures were removed and the archive
verified under the lease.

[Original B1087](../README.md) · [Elapsed and CPU results](summary.json) ·
[Raw observations](observations.tgz) · [Procedures](checks-and-procedures.tar.xz).
