# B1810 Keep a buffered generator pull within one advance

The candidate on `8db70aa47` attaches the borrowed
yield destination after reentry validation and managed frame entry.
A nested buffered pull that raises `ValueError` leaves the running
advance's destination alone. The existing generator runners own the
borrow through execute, asynchronous suspension and cleanup. Buffered
async callers return the runner's operation directly, removing one
continuation wrapper. Public yielded values and terminal exhaustion keep
their existing paths.

Windows .NET 11 and Ubuntu .NET 10 each pass 104 white-box and
381 public focused Release checks. The extended numeric sum case
attempts a nested buffered sum inside an active exception handler and
preserves the outer accumulator and exception state. CPython 3.13.2
matches the script output. Pending host I/O, cancellation, refusal and
funding checks pass on both targets.

The initial catalog used literal UTF-8 text where the comparison tool
requires base64. The verifier rejected the catalog before running a
workload. The corrected catalog encodes the same bytes and both complete
32-file images then match three independent CPython output and file goldens.

## Allocation and admission

Windows .NET 11 runs the file-length reports with 2,304 Unicode text
records across 24 files and the existing 2,048-row shipment comparison.
All allowances return to baseline after disposal.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| File lengths via sum | sync | 470,344 → 470,344 | 440,432 → 440,432 |
| File lengths via join | sync | 585,768 → 585,768 | 507,600 → 507,600 |
| Five-item shipment control | sync | 1,358,936 → 1,358,936 | 3,795,557 → 3,795,557 |
| File lengths via sum | async | 470,592 → 470,592 | 440,432 → 440,432 |
| File lengths via join | async | 586,016 → 586,016 | 507,600 → 507,600 |
| Five-item shipment control | async | 1,359,120 → 1,359,120 | 3,795,557 → 3,795,557 |

## Local timing

Ubuntu WSL runs two workers per source, with three seconds of warmup and
seven rotated timed batches per worker. Ranges span the four pairings of
worker medians. Negative changes mean less time. Warm elapsed includes
mediated RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and request work. Startup
and compilation stay outside the timed lanes. All 112 batches completed
with workers joined, fixtures removed and a verified persistent archive.
The timed host completes its file operations in memory. Pending host I/O
is covered by the focused lifetime tests.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File lengths via sum | Warm async | −16.60% to +5.96% | −11.72% to +2.57% |
| Five-item shipment control | Warm async | −10.93% to +1.63% | −10.53% to +2.39% |
| File lengths via sum | Warm sync | −2.06% to +7.62% | −1.19% to +2.08% |
| File lengths via join | Warm async | −8.14% to +1.11% | −7.75% to −0.43% |

Retain the change to preserve the outer borrow during a rejected nested
pull and remove an async wrapper. Allocation and admitted peaks match
exactly in both APIs. All elapsed ranges span zero. The async join uses
0.43–7.75% less process CPU in this run. The shipment control moves and
the async sum candidate has 24.44% elapsed spread, so these data leave
the throughput impact uncertain.

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Focused checks, catalogs and exact patches](procedures.tar.xz).
