# B1700 Retire private cursors directly

The reference is committed `0524f87c8`. Both 32-file images are verified
and differ only in the core DLL. The candidate deletes the inbound CLR
async iterable adapter, whose producers were white-box fixtures, and the
executable stack's queue and polling for deferred cursor disposal. Both
drivers share loop retirement. CSV completion releases parser scratch
directly after the final pull. A private cursor owns synchronous traversal
state and borrows Python iterators. Actual file and generator cleanup still
awaits its resource owners.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026
UTC. Each source has two processes, five seconds of warmup per process,
and seven rotated timed batches. Ranges span all pairings of process
medians. Warm elapsed includes applicable mediated RAM-backed I/O, output
and file verification and previous-result disposal. Wider process CPU
also includes fixture reset and protocol. Compilation, process startup and
final teardown are outside both clocks.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed map | Warm async | −1.60 to +8.24% | −1.07 to +7.62% | 0.64 / 9.30% |
| Generator over map | Warm async | −0.47 to +2.16% | +0.51 to +2.74% | 1.11 / 1.51% |
| Shipment callbacks | Warm async | −2.01 to −0.85% | −2.56 to 0.00% | 0.85 / 0.32% |
| Retained zip | Warm async | −2.35 to +1.13% | −3.24 to −0.23% | 2.83 / 0.72% |
| Shipment callbacks | Warm sync | −6.19 to −3.89% | −6.07 to −2.92% | 0.35 / 2.10% |
| Quoted CSV invoice | Warm sync | −2.86 to −1.26% | −2.59 to −1.06% | 1.31 / 0.33% |

Retain the deletion and the observed synchronous gains. Async timing
varies by shape. Generator-over-map CPU increases despite allocating
3,664 B less per invocation in the local Windows diagnostic. File-fed
map has high candidate worker spread. Keep both observations for the
affected-cursor checkpoint. All eight local admission peaks match, and
every run restores the shared allowance. These observations establish no
new external timing ratio.

Windows .NET 11 and Ubuntu .NET 10 each pass 204 white-box and 1,313
public Release checks. A new public regression gates two real host file
closes during `generator.close()`, verifies no subsequent output while
cleanup is pending and checks allowance reuse. Pending native pulls,
terminal refusal, retained aliases, and failing inner cursor cleanup
have independent coverage. Both complete images verify seven CPython
goldens. All 168 timed batches, both machine gates, worker joins, fixture
retirement and persistent archive verification complete.

## Delivery correction

The first full workflow passed all 18,543 public checks on each
platform. Five operand-stack regressions showed that ordinary
shrinkage must preserve entries its failing disposer has not reached.
The correction keeps that behavior. Unwinding a frame or entering an
exception handler finishes relinquishing the whole discarded suffix, in
order, before propagating the first cleanup failure. The timing table
measures the original B1700 image. The corrected `701f0b1e5` passes
the Windows and Ubuntu workflows with Release tests on both frameworks,
original stress scales and installed package consumers.
[Initial CI](https://github.com/Lokad/Lython/actions/runs/45558141051) ·
[Failed checks](initial-delivery-failure.tar.xz) ·
[Corrected delivery](https://github.com/Lokad/Lython/actions/runs/45559762705).

[Source and images](identity.json) · [Timings](quiet-summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).
