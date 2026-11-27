# B1854 Fuse asynchronous executable frame driving

[B1851](../2026-11-27-paired-shipment-api-cpu/README.md) resolves 15
nearest samples in the async per-frame interpreter driver and 14 in the
enclosing call-chain driver. This candidate executes the same instruction
prefix and call chain in one state machine. It removes the per-frame
completed task handoff and an async layer during real suspension. The
existing instruction spans, exception routing and frame retirement remain.
The patch changes no synchronous driver or memory admission estimate.

Both Windows Release targets pass 2,787 public and 1,583 white-box focused
checks, including exception routing, suspended host calls and ownership.
Both exact images match all three independent CPython output and file
goldens on Windows and Ubuntu. Peak admission matches and refunds return
to baseline in both APIs. Local CLR allocation differs by at most 6 B per
invocation. The only image difference is the candidate core DLL, against
production `9cee6f423`.

## Quiet timing

November 27, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Four workers per lane warm for at least five seconds each and supply seven
rotated batches. Ranges span pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, verification and previous-result cleanup.
Process CPU also includes fixture reset and request framing. Compilation
and startup sit outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Wide shipment | Async | −2.18% to +0.52% | −2.67% to +2.04% |
| File-fed map | Async | −6.34% to +0.51% | −7.02% to −0.51% |
| Bound shipment | Async | −3.22% to +1.44% | −1.95% to +3.91% |
| Wide shipment control | Sync | +4.32% to +4.66% | +1.76% to +5.53% |

Keep the candidate out of delivery. The two shipment async lanes span zero.
File-fed async uses less CPU across worker pairings, but its reference
elapsed workers differ by 5.92%. The synchronous control is slower in every
pairing and its source path is unchanged. The measurements leave the cause
and the representative benefit unresolved. Preserve the patch for a future
confirmation if a control or driver refinement can distinguish those effects.

All 112 batches and both quiet gates pass. Staging reuses 32 reference
files and 31 candidate files from B1847. Every worker joins, the RAM-backed
fixtures are removed and the persistent archive verifies under the lease.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patch and checks](procedures.tar.xz).
