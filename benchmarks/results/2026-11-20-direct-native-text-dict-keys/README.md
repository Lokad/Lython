# Exact string-key dictionary reads and private writes

The source comparison uses `bd94edeec` and retained `c62457d49`.
The candidate passes the known string type into the existing bucket
probe on ordinary native reads. Private record construction uses
the same hash and probe before replacing a value or appending a key.
This skips contextual hash preparation and the second shared-layout
probe. Native builders carry their active context through capacity
growth and no longer establish an ambient scope solely for string keys.
Public mutation, namespace bridges and dictionaries with protocol
keys retain their existing dispatch.

Both complete 32-file images verified all four CPython goldens.
Windows .NET 11 and Ubuntu .NET 10 passed 595 white-box and 2,407
public Release checks per platform, including duplicate names, changed
layouts, mixed keys, callback mutation, cancellation, admission refusal
and cleanup. The initial sparse test checkout omitted representative
CSV scripts and reported four failures. The exact candidate checks
include those fixtures. Local CLR allocation changes by 0.00–0.20%
and accounted peaks match on all four scripts. The shared allowance
returns to baseline.

## B1246 warm exact-image comparison

Both collections use the Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11
RC3 on November 20, 2026 UTC. Warm elapsed covers guest execution,
mediated RAM-backed file I/O, goldens and previous-result disposal.
Process CPU covers the wider worker request. Compilation, startup,
fixture reset and final teardown sit outside the guest clock. Ranges
compare the four pairings of two process medians per source.

| Workload | Elapsed change | CPU change |
| --- | ---: | ---: |
| Generated validator, successful integer parses | −4.43% to −4.10% | −4.46% to −2.72% |
| Named-selector shipment, 3,072 similar records | +0.70% to +3.41% | 0.00% to +3.66% |
| F-string shipment, 3,072 alternating shapes | −4.40% to −3.29% | −4.97% to −3.53% |
| Guarded manifest | −0.69% to +0.76% | −1.97% to −0.27% |

The validator and alternating report improve on both clocks.
The named-selector regression motivates a reverse-order repeat
alongside the validator.

## B1247 reverse-order confirmation

The two lanes reuse both exact images, reverse the workload and
worker order, and warm each process for 22 seconds. The sampled
batches target one second.

| Warm workload | Elapsed change | CPU change |
| --- | ---: | ---: |
| Generated validator, successful integer parses | −4.73% to −2.49% | −3.50% to −0.81% |
| Named-selector shipment | −1.24% to +4.05% | −0.83% to +4.02% |

The validator improves across both collections. The alternating
report improves in B1246. The named-selector range spans zero in
the repeat, with 3.59% candidate-worker spread. Retain the dictionary
change for the removed generic dispatch and the two affected
workload gains. Keep the initial adverse named workload and the
repeat as open evidence. If the named workload reproduces a clear
regression, separate its prepared selector reads and JSON
construction before changing the shared path.

All 112 initial and 56 repeat batches passed. Both collections passed
their quiet gates, joined the workers, removed the RAM fixtures and
verified the archive under the lease.

[Initial timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Repeat timing](confirmation/summary.json) · [Repeat observations](confirmation/observations.tgz) ·
[Repeat procedures](confirmation/procedures.tar.xz).
