# B1198 Inline format renderer with shared scratch

Measures `f0263f0ec` against `9e65abf0a` on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Each role used two
independent processes and at least eight seconds of warmup per process.
Five rotated rounds targeted 0.4-second requests. Ranges cover the four
pairings of process medians. Negative values favor the candidate.

| Warm workload | Managed allocation change | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: | ---: |
| Named shipment | −7.81% | +0.08% to +1.02% | −2.38% to 0.00% | 0.53% / 0.40% |
| Two nested widths | −9.24% | −2.52% to +0.66% | −3.35% to +1.15% | 0.67% / 2.57% |
| Positional shipment | −7.77% | −6.22% to −3.01% | −7.32% to −1.98% | 3.28% / 0.13% |
| Named formatting | −12.17% | −3.77% to −2.21% | −4.55% to −2.90% | 0.44% / 1.18% |
| Expanded forwarding | +0.01% | −1.36% to +0.61% | −1.20% to +2.56% | 0.62% / 1.37% |
| CSV invoice control | 0.00% | −1.11% to +2.21% | −4.88% to +3.45% | 3.17% / 0.18% |

Warm guest timing includes execution, mediated RAM-backed I/O, checks
and prior-result cleanup. Compilation, fixture reset, startup and final
teardown are outside guest elapsed timing. Process CPU covers the request
loop. Allocation is a separate local diagnostic. Compile allocation is
flat, accounted execution peaks match and cleanup restores the borrowed
allowance in all six cases. CPython here verifies semantics and goldens.

The renderer holds call operands in existing continuation state and reuses
one scratch buffer for successive expanded specs. The caller keeps borrowed
inputs funded across callbacks. Buffer capacity is admitted on first use,
then stays funded until rendering completes or unwinds. The earlier B1166
variant leased scratch separately for each nested field.

Retain the renderer change. The positional shipment and narrow named
formatting case take less elapsed and CPU in this run. The named shipment
is 0.08–1.02% slower in elapsed with lower or equal CPU. The two-width
case spans zero on both clocks. B1166's consistently higher nested CPU
did not recur. The unchanged invoice's spread limits cross-workload
attribution.

Windows .NET 11 and Ubuntu .NET 10 each passed 740 public and 88 white-box
focused Release checks. Both complete frozen images passed six workload
goldens and three CPython semantic probes in synchronous and asynchronous
execution. The probes cover retained nested specs, callback order and
recursive spec rejection. Both quiet gates and all 120 timed batches
passed. The workers joined and the persistent archive verified under
the lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Source, checks and procedure](checks-and-procedures.tar.xz) ·
[Earlier scratch comparison](../2026-11-20-method-format-continuation/README.md).
