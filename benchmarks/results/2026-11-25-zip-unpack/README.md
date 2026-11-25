# B1665 Deliver fixed zip unpacks into the execution frame

The fixed-unpack instruction can receive an exact native zip row directly in
admitted frame slots. That path removes the temporary tuple, its backing
array and its reservation. Ordinary zip consumers still receive a tuple.
Both destinations share input order and strict-tail handling in each
execution mode. The frame owns partial writes through suspension and clears
them on exhaustion or exception before any unpack target is assigned.

The reference is the production `788be8f35` image from B1663. The candidate
adds the recorded runtime patch. Both exact 32-file images match all three
CPython 3.13.2 goldens. Their 10 code objects and 415 instructions match,
including the fixed-unpack instruction in the shipment report. The initial
shipment verification exposed reversed slots. The corrected test uses
distinct aliases and checks both shorter and longer strict tails against
CPython. The original failure and patch are preserved.

Windows and Ubuntu each pass 1,023 focused Release checks per target,
including a separate test that cancels while the second source has a
pending read. The first source advances, the partial row stays private,
the file closes and the allowance returns to baseline.

The 2,048-row shipment report allocates 1,315,542 B in the Windows
diagnostic, down 181,076 B or 12.10%. Peak admission falls 128,992 B or
4.38%. Both controls keep the same peak admission. Their managed allocation
differs by at most 174 B. Every scope returns its allowance.

## Initial quiet comparison

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. On November 24,
2026 UTC, each of the four workers warms for at least five seconds and
supplies seven rotated batches with a 0.63-second target. Ranges cover
the four pairings of worker medians. Warm elapsed covers mediated RAM I/O,
output and file verification and previous-result disposal. Compile to
result adds compilation and compiled-owner disposal. Wider CPU includes
fixture reset and protocol framing. Startup is outside these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm | −9.86 to −8.28% | −8.65 to −6.81% | 0.19 / 1.56% |
| Shipment with retained zip tuples | Warm | −1.55 to +4.09% | −1.89 to +5.42% | 0.15 / 5.57% |
| Shipment callback report | Warm async | −10.58 to −8.19% | −10.96 to −7.44% | 1.25 / 1.40% |
| Quote-heavy invoice | Warm | −3.94 to −1.09% | −4.46 to −1.00% | 1.69 / 1.26% |
| Shipment callback report | Compile-to-result | −13.17 to −8.95% | −12.77 to −7.72% | 2.20 / 2.60% |

The shipment report improves in all three phases. The retained-tuple
control spans zero, with higher candidate worker variation. The invoice
control moves in the favorable direction. Both quiet gates, all 140
batches, worker joins and archive verification pass.

## Longer confirmation

The exact same images repeat the two shipment lanes on November 25, 2026 UTC
with a 0.87-second batch target. Both complete CPython goldens, 56 batches,
quiet gates, and final worker and fixture cleanup verify.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment with retained zip tuples | Warm | −2.64 to −0.03% | −2.41 to −0.25% | 1.36 / 1.30% |
| Shipment callback report | Warm | −13.84 to −10.63% | −12.71 to −10.06% | 2.17 / 1.53% |

The warm report improves again. The retained-tuple control has substantially
lower worker spread and a small favorable range. Keep the initial adverse
end of its range with the confirmation and the matching budget evidence.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tape](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Longer timings](confirmation/quiet-summary.json) ·
[Longer observations](confirmation/quiet-observations.tar.xz) ·
[Longer terminal receipt](confirmation/quiet-terminal-status.json) ·
[Longer procedures](confirmation/procedures.tar.xz).
