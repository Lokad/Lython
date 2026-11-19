# B1113 ordinary-rule control repeat

The initial frozen images reran with 14 seconds of full-request warmup
per worker. Two ordinary-rule blocks used independent processes with
opposite starting order. A third block repeated annotated rules. The
workload sources and guest elapsed boundary match the initial comparison.

| Workload | Warm elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Ordinary rules, first order | −0.82…+4.52% | 0.00…+5.32% | 1.80 / 3.52% |
| Ordinary rules, reverse order | −3.58…+2.25% | −3.04…+0.54% | 5.27 / 0.73% |
| Annotated rules | −3.22…+0.93% | −2.64…+1.31% | 2.95 / 1.30% |

The largest ordinary slowdown was 10.61% initially and 4.52% in the
repeat. Ordinary elapsed remains inconclusive. The higher process CPU
in the first repeat block and the spread in the reverse-order reference
keep ordinary rules an open control. Annotated elapsed and CPU also span
zero. Retain the framed return path as `2841f6ac2` for the removed
tuple construction and the measured captured-rule gains. Preserve the
ordinary-rule uncertainty at delivery. [Initial results](../README.md).

All 60 timed batches verified in 212.56 seconds on the same quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both quiet gates passed,
workers joined and the VM verified the archive under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
