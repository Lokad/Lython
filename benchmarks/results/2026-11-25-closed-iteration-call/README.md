# B1687 Closed iteration callback entry

The candidate routes short exact tuples from `itertools.starmap` through the
same operand entry as `map`. Bound guest methods pass their evaluated receiver
alongside the operands. Closed integer returns read eligible omitted defaults
without constructing a binder packet. Wide rows and arbitrary iterables keep
their existing argument materialization and call semantics.

## Controlled comparison

On November 25, 2026 UTC, the Azure F4as_v7 quiet VM ran Ubuntu 24.04.4 and
.NET 11. The only image difference was the core assembly, built from
`c1984f96b` with the archived source patch. Four workers ran seven measured
batches each per lane, with repeated images for both source roles. The ranges
compare all four pairings of candidate and reference medians. Negative values
mean less time. The warm boundaries include request handling, script I/O for
file workloads, output verification and result cleanup. Through result adds
compilation in the same warm worker. This is a short within-Lython diagnostic.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Bound defaulted `map`, 4,096 readings, warm | −51.41% to −48.95% | −51.72% to −49.61% |
| Bound shipment `starmap`, warm | −9.35% to −8.70% | −9.59% to −8.06% |
| Bound shipment `starmap`, warm async | −17.47% to −16.32% | −17.02% to −16.13% |
| Defaulted rules, warm | −4.50% to −2.64% | −3.13% to −1.96% |
| Ordinary definitions, warm | +0.03% to +1.15% | −0.39% to +0.55% |
| Eager annotated rules, warm | −2.18% to −0.16% | −2.10% to +0.42% |
| Defaulted rules, through result | −1.94% to +4.12% | −1.27% to +1.72% |
| Postponed annotated rules, warm | −7.58% to −5.16% | −6.98% to −3.54% |
| Quoted invoice, warm | −3.52% to −0.48% | −2.59% to +0.61% |
| Original unbound shipment `map`, warm async | +0.86% to +4.37% | +0.99% to +4.04% |

The unchanged defaulted-rule reference spread through result is 5.02%.
Both original unbound shipment CPU comparisons are adverse across
pairings. Keep that script as a control while consolidating call entry.
The direct gains justify retaining the common entry and the tuple-drain
removal with this measured regression still open.

Local Windows .NET 11 replay shows 34.13% less CLR allocation for bound
shipment, from 2,305,126 B to 1,518,464 B, at equal peak admission. The bound
scalar report allocates 94.92% less, from 276,336 B to 14,040 B. Its admitted
peak falls from 2,901 B to 2,645 B. The seven remaining cases match allocation
and admitted peaks. Every allowance returns to its original balance.

Both images match nine independent CPython 3.13.2 goldens and emit identical
code and spans for 224 executable bodies and 2,278 instructions. The focused
Release suite passes 961 public cases per Windows target and 961 public plus
120 white-box cases on Ubuntu .NET 11. Regressions exercise live defaults,
alias identity, cancellation during suspended file I/O, one close and budget
reuse. Both quiet gates pass, all 280 batches complete, and the worker joins,
temporary fixture cleanup and persistent archive are verified.

[Identities](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patches](procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
