# B1214 Factory profiling interrupted by the aggregate capture bound

Measures the source at `645a0fcd1` with the exact B1212 image
`356a93e25`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on 2026-11-20 UTC. The profile requests three factory workloads with
eight-second warm-ups and approximately five seconds of sampling each.
The inherited 14-second aggregate capture bound stops the third workload
before symbol export and post-capture verification. The eager and nested
captures complete and verify. Keep their independent partial attribution
and the original incomplete third capture.

Both quiet checks passed. All workers joined, and the persistent archive
verified under the VM lease before the guest fixtures were removed.
[B1215](confirmation/README.md) changes the capture bound to accommodate
the planned three samples and retains the same image and workloads.

[Partial attribution](partial-attribution.json) · [Original observations](observations.tgz) ·
[Terminal status](terminal-status.json) · [Source and procedures](procedures.tar.xz).
