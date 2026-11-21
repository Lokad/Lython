# B1260 expanded builtin argument spans

Compared `e7a88f57c` with `3ab6f6c81` on November 21, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The measured
source is retained as `8785193bb`. Expanded builtin calls whose binder
consumes spans pass the accumulator's live prefix directly. This
removes the exact-size argument copy. Pending work retains the values
it needs in its existing owned storage.

The shipment export reads 3,072 records from a 189,441-byte JSON file
and encodes each normalized row into a 189,440-byte JSON Lines output.
One script forwards a dictionary of two encoding options on every row.
The explicit-keyword script uses the same records, option values and
output. The report control forwards keywords to a guest function.

| Complete warm workload | Elapsed change | Process CPU change | Local CLR allocation change |
| --- | ---: | ---: | ---: |
| JSON Lines export, expanded options | −5.48% to −0.01% | −4.61% to −2.46% | −4.90% |
| JSON Lines export, explicit options | +0.46% to +1.28% | −0.52% to +0.74% | −0.01% |
| Expanded forwarding to guest helper | −2.30% to +1.93% | −1.23% to +2.16% | −0.02% |

Each range compares all pairings of two reference and two candidate
worker medians. The expanded export's elapsed range reaches near zero
with a 3.36% candidate spread. CPU improves in every pairing. The
explicit-keyword elapsed increase is preserved with its mixed CPU
result. The guest control is inconclusive. These observations support
retaining the copy elimination and the allocation reduction.

Warm elapsed includes guest execution, mediated RAM-backed file I/O,
output verification and prior-result cleanup. Process CPU covers
the complete batch request. Compilation, startup and final teardown
sit outside guest clocks. All 84 batches passed, and the campaign
completed in 276 seconds. Both quiet gates passed and the archive
verified under the lease after the workers joined.

Local allocation falls from 9,529,788 to 9,062,844 bytes per expanded
export. The accounted peak falls by 128 bytes. Both controls' accounted
peaks match, and all budgets return to baseline. Both 32-file images
pass the three CPython goldens. The patch passes 975 public and 840
white-box focused Release checks on Windows .NET 11 and Ubuntu .NET 10.
The callback regression covers suspension, cancellation, retained
result ownership and cleanup through expanded JSON options.

Staging restored two committed observation archives and verified them
by hash. It retired only the aliases of two reproducible archived core
images. Original timing evidence and both current images remain.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Archive receipt](archive-receipt.json) ·
[Checks and procedures](checks-and-procedures.tar.xz).
