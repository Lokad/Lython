# B1733 Prepare fully supplied map callbacks

The candidate moves selection of the fixed positional call to short-map
creation. All source pulls remain ordered. The existing complete callback
entry still owns direct returns, workspace borrowing, protocol fallback and
suspension. A separately instrumented build takes the prepared entry 4,096
times per shipment run and 2,048 times per file-fed map run in both modes.
Candidate and reference reach identical admitted peaks. The local CLR
allocation diagnostic is within 212 B per run across all three cases.

| Case | Warm lane | Elapsed difference | Process CPU difference | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callbacks | Sync | −5.09% to +1.16% | −4.22% to +0.99% | 3.26% / 3.21% |
| Shipment callbacks | Async | −1.81% to +1.37% | −1.62% to +1.66% | 1.05% / 2.16% |
| File-fed map | Async | −2.26% to +4.03% | −1.85% to +2.11% | 3.41% / 2.92% |
| Unchanged Unicode text | Sync | −6.92% to +11.68% | −6.68% to +10.45% | 8.42% / 10.66% |

The November 25, 2026 UTC comparison uses the Azure F4as_v7 running Ubuntu
24.04.4 and .NET 11 RC3. Reference source is `a420c3e05`. The archived
patch and 32-file image identities identify the candidate and reference.
Two workers per source warm for seven seconds each and run seven rotated
batches each. The differences span four pairings of process medians.
Negative values indicate less time. Warm elapsed includes mediated
RAM-backed file I/O, verification and result cleanup. Wider process CPU
includes framing and fixture reset.

All elapsed and CPU ranges cross zero. The width of the text control makes
the magnitude of the other differences uncertain. There is no case for
merging the extra prepared dispatch on these observations. Preserve the
patch for reconsideration if a broader design removes the intermediate
iterator result transport. The comparison gives no basis for an async
execution regression claim.

The candidate passes 192 focused Release checks on each of Windows .NET 11
and Ubuntu .NET 10, including delayed pulls, arbitrary callbacks, retained
aliases, cancellation and refusal. Three independent CPython goldens pass
per frozen image. All 112 timed batches are checked. Both VM quiet gates
pass, all workers join, and the archive verifies under the lease.

[CPU attribution](../2026-11-25-shipment-comprehension-cpu/README.md) ·
[Identity](identity.json) · [Routes](routes.json) ·
[Allocation](allocation-summary.json) · [Timing summary](quiet-summary.json) ·
[Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures and patch](procedures.tar.xz).
