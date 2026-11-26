# B1757 Profile the file-fed map control

The file-fed warm async control took more process CPU in both B1756 timing
blocks. This paired attribution uses the exact frozen `d3d69f490` reference
and `37df68bfe` candidate. The complete 32-file manifests and an independent
CPython golden verified for each worker. [B1756](../2026-11-26-direct-identity-comparisons/README.md).

The November 26, 2026 UTC profile ran on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. Each worker warmed for at least eight seconds.
The per-worker capture samples `cpu-clock` at 199 Hz in one verified
whole-request batch with mediated RAM-backed I/O, reset and cleanup.

| Profile observation | Reference | Candidate |
| --- | ---: | ---: |
| Completed requests | 10,240 | 10,240 |
| All-thread samples | 1,250 | 1,246 |
| Resolved runtime ancestry | 1,097 | 1,093 |
| Runtime-ancestry samples with unresolved leaf | 129 | 118 |
| CSV parsing or row ancestry | 478 | 488 |
| File or host I/O ancestry | 482 | 475 |
| Nearest native executable loop | 120 | 127 |
| Nearest `map` advance | 122 | 118 |
| Nearest CSV native line read | 120 | 140 |
| Nearest record publication | 116 | 130 |
| Nearest literal field description | 115 | 92 |

The ancestry groups overlap. Both profiles locate the same principal
routes and neither has a resolved comparison frame. The separate
instrumented B1756 control also makes zero comparison entry attempts.
Several CSV leaf counts shift in opposite directions. These captures
leave the earlier timing movement unexplained. Close the attribution
without removing the adverse B1756 results or attaching a gain to the
sample counts. Revisit the debt if a shared path changes or new evidence
isolates it. Both workers and profilers joined, both quiet checks passed,
and the archive verified under the lease after fixtures were removed.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
