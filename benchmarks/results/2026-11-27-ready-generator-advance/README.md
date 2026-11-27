# B1853 Share ready generator advancement

[B1851](../2026-11-27-paired-shipment-api-cpu/README.md) resolves 24
nearest samples in the async generator advance and 16 in the sync advance
on the same wide shipment script. This trial shares ready entry, send and
exception injection, completion and failure handling. A continuation takes
over only when frame execution returns an incomplete operation. Sync and
async keep their respective frame drivers. The patch adds 61 lines and
removes 35.

Both Windows Release targets pass 415 public and 55 white-box focused
checks. The checks cover suspended host operations, cancellation, refusal,
staged yields, exception behavior and awaited generator close. Both complete
images match all three independent CPython output and file goldens on
Windows and Ubuntu. Peak admission matches per case in both APIs and the
budgets return to baseline. Local CLR allocation is unchanged except for
174 B more per wide async invocation. Only the core DLL differs between
images. The reference core is production `9cee6f423`.

## Quiet timing

November 27, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds each and supply seven
rotated batches. Ranges span pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, verification and previous-result disposal.
Process CPU also includes fixture reset and request framing. Both clocks
exclude compilation and startup. The bound and file-fed scripts contain
no generator expression or definition.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Wide shipment | Async | −3.19% to +2.54% | −2.96% to +2.58% |
| File-fed map control | Async | −3.55% to +6.19% | −3.44% to +7.66% |
| Bound shipment control | Async | +2.01% to +6.24% | +4.65% to +6.12% |
| Wide shipment | Sync | −2.39% to +1.00% | −1.35% to 0.00% |

Withdraw the prototype. Wide async spans zero and the revised source adds
code. The adverse bound result is consistent across workers, with no
established cause. The file-fed candidate workers differ by 6.98% in CPU.
Keep the original generator driver and the complete observations.

All 112 timed batches and both quiet gates pass. Staging reuses 32 reference
files and 31 candidate files from B1847. Every worker joins, the RAM-backed
fixtures are removed and the persistent archive verifies under the lease.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patch and checks](procedures.tar.xz).
