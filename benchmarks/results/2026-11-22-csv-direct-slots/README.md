# B1322 Fill admitted executable slots from CSV

Fixed unpacking of an exact file-backed `csv.reader` sends the existing
operand destination to the scanner. Valid records fill those slots in
unpack order under one field admission. This removes the list, its array
and the refund added by B1321. Ordinary and starred iteration still publish
lists. The private destination stays tracked through awaited file reads.
Refused admission leaves it empty. Parser errors, arity errors, line counts
and the final non-strict quoted record retain Python behavior.

Compared `8c81fb8d2` with private `adf7a6aeb` on November 22, 2026 UTC
on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two processes
per revision warmed for at least 16 seconds each and supplied seven rotated
batches. Ranges cover all pairings of process medians. Negative changes
favor the candidate. Warm elapsed includes mediated RAM-backed I/O, goldens
and prior-result disposal. Process CPU includes request handling. Compilation,
startup and final worker cleanup sit outside the measurement.

| Complete script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII CSV meter report | Warm | −3.53 to −1.92% | −3.13 to −2.25% | 1.04 / 0.63% |
| CSV meter report with explicit float | Warm | −8.83 to −3.82% | −8.06 to −3.96% | 4.96 / 0.51% |
| Plain integer invoice control | Warm | −4.35 to +3.71% | −4.21 to +3.92% | 5.20 / 3.08% |
| Plain integer invoice control | Warm async | +0.54 to +1.83% | +1.54 to +2.55% | 1.22 / 0.06% |

Local managed allocation falls 14.15% on both typed reports and 10.49%
on the explicit-float report. Each peak falls 589,824 accounted bytes.
The invoice allocations and peak are effectively unchanged. All four budgets
refund to baseline. The regular invoice timings vary with reference process
spread. The async invoice moves against the candidate consistently. Keep
this version private pending a test of the extra native line await. Reuse
one async row-pull loop and confirm both affected cases and the async control.

Windows .NET 11 and Ubuntu .NET 10 each pass 120 white-box and 405 public
focused Release tests. The new probes cover arity errors and resume position,
starred iteration, multiline and final EOF records, numeric identity, refused
admission and cancellation. Both 32-file workers match local CPython goldens;
all 112 timed batches verify. Workers join and fixtures are removed. The
archive verifies under the lease. The first scheduled local retrieval rejected
its early timestamp before contacting the VM. The later retrieval accepted
the existing terminal archive.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-sparse-lexical-preparation/candidate-core.dll.gz).
