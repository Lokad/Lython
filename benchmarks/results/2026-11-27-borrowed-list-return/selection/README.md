# B1829 refinement read only the selected operand

The [initial trial](../README.md) tested all staged inputs for publication
before reading them in place. This refinement uses the existing closed
return proof and only borrows for a single return or a two-branch selection.
It keeps the fast result when the selected operand is already materialized.
A deferred selected value and dynamic truth use the existing copy,
publication and callback path. Both track the same generator lifetime.

The reference image matches the first experiment. Only the candidate core
changes. All four output and file goldens match independent CPython 3.13.2.
Both local Release targets pass 177 public and 18 white-box checks.
The selector regression adds an unused computed integer and confirms
the selected reference and a reentrant truth protocol in both APIs.
Execution admission matches and the allowance returns to baseline.
Median CLR allocation differs by at most 212 bytes across the eight
workload and API pairs.

Four lanes use the initial WSL settings and logical CPU affinity. Two
workers per source warm for five seconds and contribute seven rotated
timed batches. Ranges span worker-median pairs. Warm elapsed includes
mediated I/O, output checks and prior-result disposal. Process CPU also
includes fixture reset and framing.

| Complete workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-field shipment | warm | −13.40% to +18.78% | −11.89% to +12.26% |
| Three-field shipment | warm-async | −42.90% to +31.00% | −6.25% to +23.64% |
| Bound zip shipment control | warm-async | −10.28% to +99.00% | −15.23% to +56.86% |
| Five-field shipment | warm-async | −36.34% to +2.77% | −15.00% to +3.16% |

The refinement's elapsed and CPU ranges span zero for both shipment widths.
The bound zip control's same-source elapsed spread reaches 60.93%. That
variation leaves the timing inconclusive. Keep the original direct
handoff and preserve the alias and reentry regression. The refinement
removes the input scan and successful row copy, with additional borrow
and fallback handling. Keep both comparisons and move the next runtime
milestone to a larger duplicated call or iterator stage.

The retained regression passes in both APIs on the restored runtime on
Windows .NET 11 and Ubuntu WSL .NET 10. All 112 timed batches verify. The
workers join, fixtures are removed and
the persistent archive verifies.

[Frozen images](identity.json) · [Medians and spreads](summary.json) ·
[Allocation and admission](allocation-summary.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Exact patches and checks](procedures.tar.xz).
