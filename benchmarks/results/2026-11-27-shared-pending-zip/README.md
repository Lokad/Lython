# B1830 Share suspended zip row construction

The candidate on `603044737` consolidates zip's two suspended pull loops.
Fixed unpack receives its arity or exhaustion directly from the resumed
operation, removing the extra async state machine. Tuple consumers keep
their funded array through suspension. Fixed unpack keeps its existing
frame slots, and short starmap keeps its own inline prefix. The synchronous
pull loop, strict tail checks and cursor order stay in the shared core.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 210 public and 18 white-box
Release checks. The new starmap fixture starts a reentrant pull from a
source hook after the first operand has been staged, with a host-controlled
suspension before reentry. Existing delayed fixed-unpack checks cover
strict tails, cancellation and awaited cleanup. All four complete scripts
match CPython 3.13.2 output and file goldens. Their median CLR allocation
and peak admission match in both APIs, and each run returns its allowance.

## Pending allocation

A Windows .NET 11 Release probe uses the compared core DLLs. Each pull
stages one list alias, waits on a task gate, then receives the second
original alias. It checks the row order and scope cleanup. The measured
operation includes creating and releasing the gate. Six thousand warm
calls per carrier precede three batches of 20,000 calls per carrier.

| Genuinely pending pull | Reference CLR bytes per row | Candidate CLR bytes per row |
| --- | ---: | ---: |
| Fixed unpack | 440 | 304 |
| Inline operands | 424 | 424 |

## Complete-script timing

Two workers per source ran on one logical CPU in local Ubuntu WSL .NET 11.
Each warmed for five seconds and contributed seven rotated batches.
Ranges cover all four pairs of worker medians. Warm elapsed includes
mediated I/O, verification and prior-result disposal. Process CPU also
includes fixture reset and framing. Startup and compilation sit outside
these lanes. The file-fed map runs outside the changed zip path.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-field shipment | warm | −10.65% to +12.95% | −11.91% to +17.11% |
| File-fed map control | warm-async | −3.74% to +2.62% | −6.41% to +0.01% |
| Bound zip shipment | warm-async | −6.88% to +5.09% | −7.59% to +2.38% |
| Five-field shipment | warm-async | −23.25% to −1.13% | −10.51% to −5.15% |

Five-field async CPU falls 5.15–10.51% locally. Its reference elapsed
medians differ by 25.72%, and the file-fed control also trends toward less
CPU. Sync and bound async CPU ranges span zero. Keep the timing diagnostic.
The delivered change removes a duplicate continuation and saves a
measured 136 CLR bytes per genuinely suspended fixed-unpack row in the
gated fixture. All 112 timed batches verify. Workers join and guest
fixtures are removed before the persistent archive is verified.

[Images](identity.json) · [Medians and spreads](summary.json) ·
[Script allocation and admission](allocation-summary.json) ·
[Pending allocation](pending-allocation.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Exact patches and checks](procedures.tar.xz).
