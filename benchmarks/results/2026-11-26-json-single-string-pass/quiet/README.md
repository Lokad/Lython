# B1796 Quiet confirmation

The same frozen images based on 5c0fbe556 matched five CPython goldens
on the Azure F4as_v7 VM. Timing ran on November 26, 2026 UTC under
Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source warmed for five
seconds and then ran seven rotated batches targeting 440 ms. Each
range covers the four pairings of worker medians. Negative values
indicate less time. Applicable mediated RAM-backed I/O, output checks
and previous-result disposal count in elapsed. Process CPU also includes
fixture reset and framing. Startup and compilation sit outside the
measured intervals.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| JSON Lines helper, async | −1.84% to +0.69% | −3.10% to +0.79% |
| Unicode text control, sync | −6.06% to +5.04% | −5.42% to +5.79% |
| JSON service totals, sync | −2.27% to +1.11% | −2.15% to +0.63% |
| Raw Unicode JSON report, sync | +1.93% to +4.24% | +2.22% to +4.55% |

The Unicode text control has 11.13% reference elapsed and 9.13% reference
CPU spread. The raw Unicode report has tighter CPU spreads of 0.98%
and 1.28% across its candidate and reference workers. Both the report's
quiet CPU and its local and quiet elapsed increase. The JSON service
and Lines ranges include zero in the quiet run. Keep the candidate out
of production. The scan retires a full-token pass, but the comparison
does not establish a gain for the target scripts and finds an adverse
Unicode result.

All 112 batches and both quiet checks pass. Workers join and temporary
guest fixtures are removed. The observations archive verifies under
the machine lease. Staging released one duplicate B1712 core DLL after
verifying the full frozen local image, a separate compressed copy and
the persistent archive against its pushed copy.

[Images](identity.json) · [Timing and worker spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and storage verification](procedures.tar.xz).
